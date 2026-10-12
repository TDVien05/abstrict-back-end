using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Integrations.Storage;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Services.Implementations;

public sealed class AdminCompanyReviewService(
    AppDbContext dbContext,
    IFileStorage fileStorage,
    CitizenIdProtector citizenIdProtector,
    TimeProvider timeProvider) : IAdminCompanyReviewService
{
    public const int MaximumPageSize = 50;
    private const int MinimumReasonLength = 10;

    public async Task<PagedResponse<AdminCompanyListItemResponse>> ListAsync(
        ApprovalStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);

        var query = dbContext.CompanyProfiles.AsNoTracking()
            .Where(c => c.Provider.Type == ProviderType.Company);
        query = status is { } filter
            ? query.Where(c => c.Provider.ApprovalStatus == filter)
            : query.Where(c => c.Provider.ApprovalStatus == ApprovalStatus.Submitted || c.Provider.ApprovalStatus == ApprovalStatus.UnderReview);

        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .Select(c => new
            {
                c.ProviderId,
                c.LegalName,
                c.TaxCode,
                c.OwnerUser.PhoneNumber,
                c.Provider.ApprovalStatus,
                SubmittedAtUtc = dbContext.ProviderApprovalReviews
                    .Where(r => r.ProviderId == c.ProviderId)
                    .Max(r => (DateTimeOffset?)r.SubmittedAtUtc)
            })
            .OrderBy(x => x.SubmittedAtUtc)
            .ThenBy(x => x.ProviderId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<AdminCompanyListItemResponse>(
            items.Select(x => new AdminCompanyListItemResponse(
                x.ProviderId, x.LegalName, x.TaxCode, x.PhoneNumber, x.ApprovalStatus.ToString(), x.SubmittedAtUtc)).ToList(),
            page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<AdminCompanyDetailResponse> GetAsync(Guid adminUserId, Guid providerId, CancellationToken cancellationToken)
    {
        var profile = await LoadProfileAsync(providerId, track: false, cancellationToken);
        dbContext.AuditLogs.Add(NewAudit(adminUserId, "company.viewed", providerId));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildDetailAsync(profile, cancellationToken);
    }

    public async Task<AdminCompanyDetailResponse> StartReviewAsync(Guid adminUserId, Guid providerId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockProviderAsync(providerId, cancellationToken);
        var profile = await LoadProfileAsync(providerId, track: true, cancellationToken);

        if (profile.Provider.ApprovalStatus == ApprovalStatus.Submitted)
        {
            var now = timeProvider.GetUtcNow();
            var review = await LatestPendingReviewAsync(providerId, cancellationToken);
            profile.Provider.ApprovalStatus = ApprovalStatus.UnderReview;
            profile.Provider.UpdatedAtUtc = now;
            review.AssignedAdminUserId = adminUserId;
            review.ReviewStartedAtUtc = now;
            review.UpdatedAtUtc = now;
            dbContext.AuditLogs.Add(NewAudit(adminUserId, "company.review_started", providerId));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (profile.Provider.ApprovalStatus != ApprovalStatus.UnderReview)
        {
            throw NotPending("Hồ sơ này không ở trạng thái chờ duyệt.");
        }

        await transaction.CommitAsync(cancellationToken);
        return await BuildDetailAsync(profile, cancellationToken);
    }

    public async Task<AdminCompanyDetailResponse> DecideAsync(
        Guid adminUserId, Guid providerId, KycDecisionRequest request, CancellationToken cancellationToken)
    {
        if (request.Decision is not (ProviderApprovalDecision.Approved or ProviderApprovalDecision.Rejected or ProviderApprovalDecision.ChangesRequested))
            throw new KycFlowException(StatusCodes.Status400BadRequest, "INVALID_DECISION", "Quyết định phải là duyệt, từ chối hoặc yêu cầu chỉnh sửa.");

        var reason = request.Reason?.Trim();
        if (request.Decision != ProviderApprovalDecision.Approved && (reason is null || reason.Length < MinimumReasonLength))
            throw new KycFlowException(StatusCodes.Status400BadRequest, "REASON_REQUIRED", $"Cần nêu lý do rõ ràng (ít nhất {MinimumReasonLength} ký tự) để công ty chỉnh sửa.");

        var flaggedTypes = (request.Decision == ProviderApprovalDecision.Approved ? null : request.RejectedDocumentTypes)
            ?.Distinct().ToList() ?? [];
        if (flaggedTypes.Any(type => !CompanyDocument.AllTypes.Contains(type)))
            throw new KycFlowException(StatusCodes.Status400BadRequest, "INVALID_DOCUMENT_TYPE", "Loại giấy tờ không hợp lệ.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockProviderAsync(providerId, cancellationToken);
        var profile = await LoadProfileAsync(providerId, track: true, cancellationToken);
        if (profile.Provider.ApprovalStatus is not (ApprovalStatus.Submitted or ApprovalStatus.UnderReview))
            throw NotPending("Hồ sơ này không ở trạng thái chờ duyệt (có thể đã được xử lý).");

        var now = timeProvider.GetUtcNow();
        var review = await LatestPendingReviewAsync(providerId, cancellationToken);
        var documents = await dbContext.VerificationDocuments.Where(x => x.ProviderId == providerId).ToListAsync(cancellationToken);

        if (request.Decision == ProviderApprovalDecision.Approved)
        {
            if (!CompanyOnboardingPolicy.HasAllRequiredDocuments(profile.LiabilityCoverageVnd, documents.Select(x => x.Type)))
                throw new KycFlowException(StatusCodes.Status409Conflict, "COMPANY_DOCUMENTS_MISSING", "Hồ sơ chưa đủ giấy tờ để duyệt.");
            foreach (var document in documents)
            {
                document.Status = VerificationStatus.Verified;
                document.VerifiedAtUtc = now;
                document.VerifiedByAdminUserId = adminUserId;
                document.RejectionReason = null;
                document.UpdatedAtUtc = now;
            }
            // Duyệt hồ sơ chưa bật nhận đơn: đặt lịch với công ty chưa được hỗ trợ.
            profile.Provider.ApprovalStatus = ApprovalStatus.Approved;
        }
        else
        {
            foreach (var document in documents.Where(d => flaggedTypes.Contains(d.Type)))
            {
                document.Status = VerificationStatus.Rejected;
                document.RejectionReason = reason;
                document.UpdatedAtUtc = now;
            }
            profile.Provider.ApprovalStatus = request.Decision == ProviderApprovalDecision.Rejected
                ? ApprovalStatus.Rejected
                : ApprovalStatus.Draft;
        }

        profile.Provider.UpdatedAtUtc = now;
        review.Decision = request.Decision;
        review.DecisionReason = reason;
        review.DecidedByAdminUserId = adminUserId;
        review.AssignedAdminUserId ??= adminUserId;
        review.DecidedAtUtc = now;
        review.UpdatedAtUtc = now;
        dbContext.AuditLogs.Add(NewAudit(adminUserId, $"company.{request.Decision.ToString().ToLowerInvariant()}", providerId));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await BuildDetailAsync(profile, cancellationToken);
    }

    public async Task<KycDocumentContent?> OpenDocumentAsync(Guid adminUserId, Guid providerId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await dbContext.VerificationDocuments.AsNoTracking()
            .Where(x => x.Id == documentId && x.ProviderId == providerId && x.Provider.Type == ProviderType.Company)
            .SingleOrDefaultAsync(cancellationToken);
        if (document is null)
            return null;

        var stream = await fileStorage.OpenReadAsync(document.ObjectKey, cancellationToken);
        if (stream is null)
            return null;

        dbContext.AuditLogs.Add(NewAudit(adminUserId, "company.document_viewed", providerId));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new KycDocumentContent(stream, CompanyDocument.ContentTypeFromKey(document.ObjectKey));
    }

    private async Task<CompanyProfile> LoadProfileAsync(Guid providerId, bool track, CancellationToken cancellationToken)
    {
        var query = dbContext.CompanyProfiles
            .Include(x => x.Provider).Include(x => x.Representative)
            .Include(x => x.OwnerUser).ThenInclude(x => x.CustomerProfile)
            .AsQueryable();
        if (!track)
            query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(x => x.ProviderId == providerId && x.Provider.Type == ProviderType.Company, cancellationToken)
            ?? throw new KycFlowException(StatusCodes.Status404NotFound, "COMPANY_NOT_FOUND", "Không tìm thấy hồ sơ công ty.");
    }

    private Task LockProviderAsync(Guid providerId, CancellationToken cancellationToken) =>
        dbContext.Providers
            .FromSqlInterpolated($"SELECT * FROM providers WHERE id = {providerId} FOR UPDATE")
            .ToListAsync(cancellationToken);

    private async Task<ProviderApprovalReview> LatestPendingReviewAsync(Guid providerId, CancellationToken cancellationToken) =>
        await dbContext.ProviderApprovalReviews
            .Where(x => x.ProviderId == providerId && x.Decision == ProviderApprovalDecision.Pending)
            .OrderByDescending(x => x.SubmittedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw NotPending("Hồ sơ này không có yêu cầu duyệt đang chờ.");

    private async Task<AdminCompanyDetailResponse> BuildDetailAsync(CompanyProfile profile, CancellationToken cancellationToken)
    {
        var snapshot = await CompanyOnboardingService.LoadSnapshotAsync(dbContext, profile.ProviderId, cancellationToken);
        var representative = profile.Representative;
        return new AdminCompanyDetailResponse(
            profile.ProviderId,
            profile.Provider.ApprovalStatus.ToString(),
            profile.OwnerUser.PhoneNumber,
            profile.OwnerUser.CustomerProfile?.FullName ?? string.Empty,
            CompanyOnboardingService.ToCompanyResponse(profile),
            representative is null
                ? null
                : new AdminCompanyRepresentativeResponse(
                    representative.FullName, representative.JobTitle, representative.WorkEmail, representative.PhoneNumber,
                    citizenIdProtector.Unprotect(representative.IdentityNumberEncrypted)),
            snapshot.ServiceAreas,
            CompanyOnboardingPolicy.RequiredDocumentTypes(profile.LiabilityCoverageVnd),
            snapshot.DocumentResponses(),
            snapshot.Feedback(),
            snapshot.SubmittedAtUtc);
    }

    private static KycFlowException NotPending(string message) =>
        new(StatusCodes.Status409Conflict, "COMPANY_REVIEW_INVALID_STATE", message);

    private static AuditLog NewAudit(Guid adminUserId, string action, Guid providerId) => new()
    {
        ActorUserId = adminUserId,
        Action = action,
        EntityType = nameof(Provider),
        EntityId = providerId
    };
}
