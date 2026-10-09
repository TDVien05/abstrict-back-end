using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Integrations.Storage;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Services.Implementations;

public sealed class AdminKycService(
    AppDbContext dbContext,
    IFileStorage fileStorage,
    CitizenIdProtector citizenIdProtector,
    TimeProvider timeProvider) : IAdminKycService
{
    public const int MaximumPageSize = 50;
    private const int MinimumReasonLength = 10;

    public async Task<PagedResponse<AdminKycListItemResponse>> ListAsync(
        ApprovalStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);

        var query = dbContext.FreelancerProfiles.AsNoTracking()
            .Where(f => f.Provider.Type == ProviderType.Freelancer);
        query = status is { } filter
            ? query.Where(f => f.Provider.ApprovalStatus == filter)
            : query.Where(f => f.Provider.ApprovalStatus == ApprovalStatus.Submitted || f.Provider.ApprovalStatus == ApprovalStatus.UnderReview);

        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .Select(f => new
            {
                f.ProviderId,
                f.LegalFullName,
                f.User.PhoneNumber,
                f.Provider.ApprovalStatus,
                SubmittedAtUtc = dbContext.ProviderApprovalReviews
                    .Where(r => r.ProviderId == f.ProviderId)
                    .Max(r => (DateTimeOffset?)r.SubmittedAtUtc)
            })
            .OrderBy(x => x.SubmittedAtUtc)
            .ThenBy(x => x.ProviderId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<AdminKycListItemResponse>(
            items.Select(x => new AdminKycListItemResponse(x.ProviderId, x.LegalFullName, x.PhoneNumber, x.ApprovalStatus.ToString(), x.SubmittedAtUtc)).ToList(),
            page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<AdminKycDetailResponse> GetAsync(Guid adminUserId, Guid providerId, CancellationToken cancellationToken)
    {
        var profile = await LoadProfileAsync(providerId, track: false, cancellationToken);
        dbContext.AuditLogs.Add(NewAudit(adminUserId, "kyc.viewed", providerId));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildDetailAsync(profile, cancellationToken);
    }

    public async Task<AdminKycDetailResponse> StartReviewAsync(Guid adminUserId, Guid providerId, CancellationToken cancellationToken)
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
            dbContext.AuditLogs.Add(NewAudit(adminUserId, "kyc.review_started", providerId));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (profile.Provider.ApprovalStatus != ApprovalStatus.UnderReview)
        {
            throw new KycFlowException(StatusCodes.Status409Conflict, "KYC_NOT_PENDING", "Hồ sơ này không ở trạng thái chờ duyệt.");
        }

        await transaction.CommitAsync(cancellationToken);
        return await BuildDetailAsync(profile, cancellationToken);
    }

    public async Task<AdminKycDetailResponse> DecideAsync(
        Guid adminUserId, Guid providerId, KycDecisionRequest request, CancellationToken cancellationToken)
    {
        if (request.Decision is not (ProviderApprovalDecision.Approved or ProviderApprovalDecision.Rejected or ProviderApprovalDecision.ChangesRequested))
            throw new KycFlowException(StatusCodes.Status400BadRequest, "INVALID_DECISION", "Quyết định phải là duyệt, từ chối hoặc yêu cầu chỉnh sửa.");

        var reason = request.Reason?.Trim();
        if (request.Decision != ProviderApprovalDecision.Approved && (reason is null || reason.Length < MinimumReasonLength))
            throw new KycFlowException(StatusCodes.Status400BadRequest, "REASON_REQUIRED", $"Cần nêu lý do rõ ràng (ít nhất {MinimumReasonLength} ký tự) để freelancer chỉnh sửa.");

        var flaggedTypes = (request.Decision == ProviderApprovalDecision.Approved ? null : request.RejectedDocumentTypes)
            ?.Distinct().ToList() ?? [];
        if (flaggedTypes.Any(type => !FreelancerKycService.RequiredDocumentTypes.Contains(type)))
            throw new KycFlowException(StatusCodes.Status400BadRequest, "INVALID_DOCUMENT_TYPE", "Loại giấy tờ không hợp lệ.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockProviderAsync(providerId, cancellationToken);
        var profile = await LoadProfileAsync(providerId, track: true, cancellationToken);
        if (profile.Provider.ApprovalStatus is not (ApprovalStatus.Submitted or ApprovalStatus.UnderReview))
            throw new KycFlowException(StatusCodes.Status409Conflict, "KYC_NOT_PENDING", "Hồ sơ này không ở trạng thái chờ duyệt (có thể đã được xử lý).");

        var now = timeProvider.GetUtcNow();
        var review = await LatestPendingReviewAsync(providerId, cancellationToken);
        var documents = await dbContext.VerificationDocuments.Where(x => x.ProviderId == providerId).ToListAsync(cancellationToken);

        if (request.Decision == ProviderApprovalDecision.Approved)
        {
            if (FreelancerKycService.RequiredDocumentTypes.Any(type => documents.All(d => d.Type != type)))
                throw new KycFlowException(StatusCodes.Status409Conflict, "KYC_DOCUMENTS_MISSING", "Hồ sơ chưa đủ giấy tờ để duyệt.");
            foreach (var document in documents)
            {
                document.Status = VerificationStatus.Verified;
                document.VerifiedAtUtc = now;
                document.VerifiedByAdminUserId = adminUserId;
                document.RejectionReason = null;
                document.UpdatedAtUtc = now;
            }
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
        dbContext.AuditLogs.Add(NewAudit(adminUserId, $"kyc.{request.Decision.ToString().ToLowerInvariant()}", providerId));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await BuildDetailAsync(profile, cancellationToken);
    }

    public async Task<KycDocumentContent?> OpenDocumentAsync(Guid adminUserId, Guid providerId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await dbContext.VerificationDocuments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == documentId && x.ProviderId == providerId, cancellationToken);
        if (document is null)
            return null;

        var stream = await fileStorage.OpenReadAsync(document.ObjectKey, cancellationToken);
        if (stream is null)
            return null;

        dbContext.AuditLogs.Add(NewAudit(adminUserId, "kyc.document_viewed", providerId));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new KycDocumentContent(stream, KycImage.ContentTypeFromKey(document.ObjectKey));
    }

    private async Task<FreelancerProfile> LoadProfileAsync(Guid providerId, bool track, CancellationToken cancellationToken)
    {
        var query = dbContext.FreelancerProfiles.Include(x => x.Provider).Include(x => x.User).AsQueryable();
        if (!track)
            query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(x => x.ProviderId == providerId, cancellationToken)
            ?? throw new KycFlowException(StatusCodes.Status404NotFound, "KYC_NOT_FOUND", "Không tìm thấy hồ sơ freelancer.");
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
        ?? throw new KycFlowException(StatusCodes.Status409Conflict, "KYC_NOT_PENDING", "Hồ sơ này không có yêu cầu duyệt đang chờ.");

    private async Task<AdminKycDetailResponse> BuildDetailAsync(FreelancerProfile profile, CancellationToken cancellationToken)
    {
        var documents = await dbContext.VerificationDocuments.AsNoTracking()
            .Where(x => x.ProviderId == profile.ProviderId).OrderBy(x => x.Type).ToListAsync(cancellationToken);
        var reviews = await dbContext.ProviderApprovalReviews.AsNoTracking()
            .Where(x => x.ProviderId == profile.ProviderId).OrderByDescending(x => x.SubmittedAtUtc).ToListAsync(cancellationToken);

        return new AdminKycDetailResponse(
            profile.ProviderId,
            profile.Provider.ApprovalStatus.ToString(),
            profile.User.PhoneNumber,
            profile.LegalFullName,
            profile.DateOfBirth,
            profile.Gender.ToString(),
            string.IsNullOrEmpty(profile.CitizenIdNumberProtected) ? string.Empty : citizenIdProtector.Unprotect(profile.CitizenIdNumberProtected),
            profile.PermanentAddress,
            profile.CurrentAddress,
            profile.ExperienceYears,
            reviews.FirstOrDefault()?.SubmittedAtUtc,
            KycMapping.ToFeedback(reviews.FirstOrDefault(x => x.DecidedAtUtc.HasValue)),
            documents.Select(x => x.ToResponse()).ToList());
    }

    private static AuditLog NewAudit(Guid adminUserId, string action, Guid providerId) => new()
    {
        ActorUserId = adminUserId,
        Action = action,
        EntityType = nameof(Provider),
        EntityId = providerId
    };
}
