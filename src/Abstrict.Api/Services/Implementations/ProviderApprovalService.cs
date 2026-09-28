using System.Text.Json;
using Abstrict.Api.Common;
using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Integrations.Storage;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Options;
using Abstrict.Api.Repositories.Interfaces;
using Abstrict.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Abstrict.Api.Services.Implementations;

public sealed class ProviderApprovalService(
    AppDbContext dbContext,
    IFreelancerApplicationRepository applicationRepository,
    IVerificationDocumentRepository documentRepository,
    IIdentityAttemptRepository attemptRepository,
    IApplicationSubmissionRepository submissionRepository,
    IUnitOfWork unitOfWork,
    IPrivateFileStorage fileStorage,
    IOptions<KycOptions> options,
    TimeProvider timeProvider) : IProviderApprovalService
{
    private readonly KycOptions _kyc = options.Value;

    public async Task<PagedResponse<AdminFreelancerApplicationSummaryResponse>> ListAsync(string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var applications = await applicationRepository.ListByStatusAsync(status, page, pageSize, cancellationToken);
        var total = await applicationRepository.CountByStatusAsync(status, cancellationToken);

        var userIds = applications.Select(x => x.UserId).Distinct().ToList();
        var users = await dbContext.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var items = new List<AdminFreelancerApplicationSummaryResponse>(applications.Count);
        foreach (var application in applications)
        {
            var latest = await submissionRepository.GetLatestAsync(application.Id, cancellationToken);
            var attempt = await attemptRepository.GetCurrentAsync(application.Id, track: false, cancellationToken);
            users.TryGetValue(application.UserId, out var user);

            items.Add(new AdminFreelancerApplicationSummaryResponse(
                application.Id,
                latest?.Id ?? Guid.Empty,
                latest?.Version ?? 0,
                application.Status.ToString(),
                latest?.Decision.ToString() ?? ProviderApprovalDecision.Pending.ToString(),
                application.UserId,
                user?.PhoneNumber ?? string.Empty,
                application.LegalFullName,
                attempt?.DocumentNumberLast4 is { Length: > 0 } last4 ? $"********{last4}" : null,
                application.SubmittedAtUtc ?? application.UpdatedAtUtc,
                latest?.ReviewStartedAtUtc,
                latest?.DecidedAtUtc));
        }

        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return new PagedResponse<AdminFreelancerApplicationSummaryResponse>(items, page, pageSize, total, totalPages);
    }

    public async Task<AdminFreelancerApplicationDetailResponse?> GetDetailAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await applicationRepository.GetByIdAsync(applicationId, track: false, cancellationToken);
        if (application is null)
            return null;

        return await BuildDetailAsync(application, cancellationToken);
    }

    public async Task<DocumentContentResult?> GetDocumentContentAsync(Guid applicationId, Guid documentId, string? ipAddress, CancellationToken cancellationToken)
    {
        var document = await documentRepository.GetAsync(documentId, track: false, cancellationToken);
        if (document is null || document.ApplicationId != applicationId)
            return null;

        var stream = await fileStorage.OpenReadAsync(document.ObjectKey, cancellationToken);
        if (stream is null)
            return null;

        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorUserId = null,
            Action = "admin.view_document",
            EntityType = nameof(VerificationDocument),
            EntityId = document.Id,
            IpAddress = ipAddress,
            CreatedAtUtc = timeProvider.GetUtcNow(),
            UpdatedAtUtc = timeProvider.GetUtcNow()
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new DocumentContentResult(stream, document.ContentType ?? "application/octet-stream", document.OriginalFileName);
    }

    public async Task<AdminFreelancerApplicationDetailResponse?> StartReviewAsync(Guid applicationId, int submissionVersion, Guid adminUserId, CancellationToken cancellationToken)
    {
        var application = await applicationRepository.GetByIdAsync(applicationId, track: true, cancellationToken);
        if (application is null)
            return null;

        var submission = await submissionRepository.GetByVersionAsync(applicationId, submissionVersion, track: true, cancellationToken);
        if (submission is null)
            throw new ApiFlowException(StatusCodes.Status404NotFound, "SUBMISSION_NOT_FOUND", "Không tìm thấy phiên bản hồ sơ đã gửi.");
        if (submission.Decision != ProviderApprovalDecision.Pending)
            throw new ApiFlowException(StatusCodes.Status409Conflict, "STALE_APPLICATION_VERSION", "Hồ sơ này đã được xử lý.");

        var now = timeProvider.GetUtcNow();
        var provider = await dbContext.Providers.SingleAsync(x => x.Id == application.ProviderId, cancellationToken);
        var review = await dbContext.ProviderApprovalReviews
            .Where(x => x.ProviderId == application.ProviderId && x.Decision == ProviderApprovalDecision.Pending)
            .OrderByDescending(x => x.SubmittedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (review is not null)
        {
            review.AssignedAdminUserId = adminUserId;
            review.ReviewStartedAtUtc = now;
            review.UpdatedAtUtc = now;
        }

        submission.Status = FreelancerApplicationStatus.UnderReview;
        submission.ReviewStartedAtUtc = now;
        submission.UpdatedAtUtc = now;

        application.Status = FreelancerApplicationStatus.UnderReview;
        application.UpdatedAtUtc = now;

        provider.ApprovalStatus = ApprovalStatus.UnderReview;
        provider.UpdatedAtUtc = now;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildDetailAsync(application, cancellationToken);
    }

    public async Task<AdminFreelancerApplicationDetailResponse?> DecideAsync(Guid applicationId, AdminApplicationDecisionRequest request, Guid adminUserId, CancellationToken cancellationToken)
    {
        var decision = request.Decision ?? throw new ApiFlowException(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Thiếu quyết định thẩm định.");
        if (decision == ProviderApprovalDecision.Pending)
            throw new ApiFlowException(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Quyết định không hợp lệ.");

        var application = await applicationRepository.GetByIdAsync(applicationId, track: true, cancellationToken);
        if (application is null)
            return null;

        var submission = await submissionRepository.GetByVersionAsync(applicationId, request.SubmissionVersion, track: true, cancellationToken);
        if (submission is null)
            throw new ApiFlowException(StatusCodes.Status404NotFound, "SUBMISSION_NOT_FOUND", "Không tìm thấy phiên bản hồ sơ đã gửi.");
        if (submission.Decision != ProviderApprovalDecision.Pending)
            throw new ApiFlowException(StatusCodes.Status409Conflict, "STALE_APPLICATION_VERSION", "Hồ sơ này đã được xử lý.");

        var now = timeProvider.GetUtcNow();
        var provider = await dbContext.Providers.SingleAsync(x => x.Id == application.ProviderId, cancellationToken);

        submission.Decision = decision;
        submission.DecidedAtUtc = now;
        submission.DecisionReason = request.Reason;
        submission.ReviewedByAdminUserId = adminUserId;
        submission.RequestedChangesJson = request.RequestedChanges is { Count: > 0 }
            ? JsonSerializer.Serialize(request.RequestedChanges)
            : null;
        submission.UpdatedAtUtc = now;

        switch (decision)
        {
            case ProviderApprovalDecision.Approved:
                submission.Status = FreelancerApplicationStatus.Approved;
                application.Status = FreelancerApplicationStatus.Approved;
                application.LastDecisionReason = null;
                provider.ApprovalStatus = ApprovalStatus.Approved;
                await ApplyApprovedSnapshotAsync(application, cancellationToken);
                break;
            case ProviderApprovalDecision.ChangesRequested:
                submission.Status = FreelancerApplicationStatus.ChangesRequested;
                application.Status = FreelancerApplicationStatus.ChangesRequested;
                application.LastDecisionReason = request.Reason;
                provider.ApprovalStatus = ApprovalStatus.Draft;
                break;
            default:
                submission.Status = FreelancerApplicationStatus.Rejected;
                application.Status = FreelancerApplicationStatus.Rejected;
                application.LastDecisionReason = request.Reason;
                provider.ApprovalStatus = ApprovalStatus.Rejected;
                await ReleaseIdentityClaimAsync(application.Id, now, cancellationToken);
                break;
        }

        provider.IsAcceptingBookings = false;
        provider.UpdatedAtUtc = now;
        application.UpdatedAtUtc = now;
        application.Version++;

        var review = await dbContext.ProviderApprovalReviews
            .Where(x => x.ProviderId == application.ProviderId && x.Decision == ProviderApprovalDecision.Pending)
            .OrderByDescending(x => x.SubmittedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (review is not null)
        {
            review.Decision = decision;
            review.DecidedByAdminUserId = adminUserId;
            review.DecidedAtUtc = now;
            review.DecisionReason = request.Reason;
            review.RequestedChangesJson = submission.RequestedChangesJson;
            review.UpdatedAtUtc = now;
        }

        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorUserId = adminUserId,
            Action = $"admin.application_{decision.ToString().ToLowerInvariant()}",
            EntityType = nameof(FreelancerApplication),
            EntityId = application.Id,
            AfterJson = JsonSerializer.Serialize(new { Decision = decision.ToString(), request.SubmissionVersion }),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildDetailAsync(application, cancellationToken);
    }

    private async Task ApplyApprovedSnapshotAsync(FreelancerApplication application, CancellationToken cancellationToken)
    {
        var attempt = await attemptRepository.GetCurrentAsync(application.Id, track: false, cancellationToken);
        var profile = await dbContext.FreelancerProfiles.SingleOrDefaultAsync(x => x.ProviderId == application.ProviderId, cancellationToken);

        if (profile is null)
        {
            profile = new FreelancerProfile
            {
                ProviderId = application.ProviderId,
                UserId = application.UserId,
                LegalFullName = application.LegalFullName ?? application.ConfirmedIdentityFullName ?? "Chưa cập nhật",
                DateOfBirth = application.DateOfBirth ?? DateOnly.FromDateTime(new DateTime(1900, 1, 1)),
                Gender = application.Gender ?? Gender.PreferNotToSay,
                PermanentAddress = application.PermanentAddress ?? string.Empty,
                CurrentAddress = application.CurrentAddress ?? string.Empty,
                ExperienceYears = application.ExperienceYears ?? 0,
                FaceMatchScore = attempt?.FaceScore,
                CreatedAtUtc = timeProvider.GetUtcNow(),
                UpdatedAtUtc = timeProvider.GetUtcNow()
            };
            dbContext.FreelancerProfiles.Add(profile);
        }
        else
        {
            profile.LegalFullName = application.LegalFullName ?? profile.LegalFullName;
            if (application.DateOfBirth is not null) profile.DateOfBirth = application.DateOfBirth.Value;
            if (application.Gender is not null) profile.Gender = application.Gender.Value;
            profile.PermanentAddress = application.PermanentAddress ?? profile.PermanentAddress;
            profile.CurrentAddress = application.CurrentAddress ?? profile.CurrentAddress;
            profile.ExperienceYears = application.ExperienceYears ?? profile.ExperienceYears;
            profile.FaceMatchScore = attempt?.FaceScore;
            profile.UpdatedAtUtc = timeProvider.GetUtcNow();
        }

        await SyncServiceAreasAsync(application, cancellationToken);
        await SyncServiceCategoriesAsync(application, cancellationToken);
        await SyncBankAccountAsync(application, cancellationToken);
    }

    private async Task SyncServiceAreasAsync(FreelancerApplication application, CancellationToken cancellationToken)
    {
        var areaIds = FreelancerOnboardingMapper.ReadIds(application.ServiceAreaIdsJson);
        if (areaIds.Count == 0)
            return;

        var existing = await dbContext.ProviderServiceAreas
            .Where(x => x.ProviderId == application.ProviderId)
            .ToListAsync(cancellationToken);
        dbContext.ProviderServiceAreas.RemoveRange(existing);
        foreach (var areaId in areaIds)
        {
            dbContext.ProviderServiceAreas.Add(new ProviderServiceArea
            {
                ProviderId = application.ProviderId,
                ServiceAreaId = areaId,
                CreatedAtUtc = timeProvider.GetUtcNow(),
                UpdatedAtUtc = timeProvider.GetUtcNow()
            });
        }
    }

    private async Task SyncServiceCategoriesAsync(FreelancerApplication application, CancellationToken cancellationToken)
    {
        var categoryIds = FreelancerOnboardingMapper.ReadIds(application.ServiceCategoryIdsJson);
        if (categoryIds.Count == 0)
            return;

        var existing = await dbContext.ProviderServices
            .Where(x => x.ProviderId == application.ProviderId)
            .ToListAsync(cancellationToken);
        dbContext.ProviderServices.RemoveRange(existing);
        foreach (var categoryId in categoryIds)
        {
            dbContext.ProviderServices.Add(new ProviderService
            {
                ProviderId = application.ProviderId,
                ServiceCategoryId = categoryId,
                HourlyRateVnd = 0,
                IsActive = true,
                CreatedAtUtc = timeProvider.GetUtcNow(),
                UpdatedAtUtc = timeProvider.GetUtcNow()
            });
        }
    }

    private async Task SyncBankAccountAsync(FreelancerApplication application, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(application.BankAccountNumberEncrypted) || string.IsNullOrWhiteSpace(application.BankCode))
            return;

        var account = await dbContext.BankAccounts
            .FirstOrDefaultAsync(x => x.ProviderId == application.ProviderId, cancellationToken);
        if (account is null)
        {
            dbContext.BankAccounts.Add(new BankAccount
            {
                ProviderId = application.ProviderId,
                BankCode = application.BankCode,
                AccountNumberEncrypted = application.BankAccountNumberEncrypted,
                AccountHolderName = application.BankAccountHolderName ?? application.ConfirmedIdentityFullName ?? string.Empty,
                IsVerified = false,
                IsDefault = true,
                CreatedAtUtc = timeProvider.GetUtcNow(),
                UpdatedAtUtc = timeProvider.GetUtcNow()
            });
        }
        else
        {
            account.BankCode = application.BankCode;
            account.AccountNumberEncrypted = application.BankAccountNumberEncrypted;
            account.AccountHolderName = application.BankAccountHolderName ?? account.AccountHolderName;
            account.UpdatedAtUtc = timeProvider.GetUtcNow();
        }
    }

    private async Task ReleaseIdentityClaimAsync(Guid applicationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var claim = await dbContext.IdentityClaims
            .SingleOrDefaultAsync(x => x.ApplicationId == applicationId && x.Status == IdentityClaimStatus.Reserved, cancellationToken);
        if (claim is not null)
        {
            claim.Status = IdentityClaimStatus.Released;
            claim.ReleasedAtUtc = now;
            claim.UpdatedAtUtc = now;
        }
    }

    private async Task<AdminFreelancerApplicationDetailResponse> BuildDetailAsync(FreelancerApplication application, CancellationToken cancellationToken)
    {
        var attempt = await attemptRepository.GetCurrentAsync(application.Id, track: false, cancellationToken);
        var documents = await documentRepository.ListByApplicationAsync(application.Id, track: false, cancellationToken);
        var latest = await submissionRepository.GetLatestAsync(application.Id, cancellationToken);
        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == application.UserId, cancellationToken);

        var onboarding = FreelancerOnboardingMapper.ToResponse(application, attempt, documents, latest, _kyc.Policy);
        var submission = onboarding.LatestSubmission ?? new FreelancerSubmissionSummaryResponse(
            Guid.Empty, 0, application.Status.ToString(), ProviderApprovalDecision.Pending.ToString(),
            application.SubmittedAtUtc ?? application.UpdatedAtUtc, null, null, null);

        return new AdminFreelancerApplicationDetailResponse(
            application.Id,
            application.ProviderId,
            application.Version,
            application.Status.ToString(),
            application.CurrentStep.ToString(),
            application.UserId,
            user?.PhoneNumber ?? string.Empty,
            onboarding.Personal,
            onboarding.Identity,
            onboarding.Documents,
            onboarding.SkillsAndBank,
            submission,
            application.LastDecisionReason);
    }
}
