using System.Security.Cryptography;
using System.Text;
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

public sealed class FreelancerOnboardingService(
    AppDbContext dbContext,
    IFreelancerApplicationRepository applicationRepository,
    IVerificationDocumentRepository documentRepository,
    IIdentityAttemptRepository attemptRepository,
    IKycOperationRepository operationRepository,
    IApplicationSubmissionRepository submissionRepository,
    IIdentityClaimRepository claimRepository,
    IKycConsentRepository consentRepository,
    IUnitOfWork unitOfWork,
    IPrivateFileStorage fileStorage,
    ISensitiveDataProtector dataProtector,
    IIdentityFingerprintService fingerprintService,
    IOptions<KycOptions> options,
    TimeProvider timeProvider) : IFreelancerOnboardingService
{
    private readonly KycOptions _kyc = options.Value;
    private static readonly HashSet<FreelancerApplicationStatus> EditableStatuses =
        [FreelancerApplicationStatus.Draft, FreelancerApplicationStatus.ChangesRequested];

    public async Task<FreelancerOnboardingResponse> GetOnboardingAsync(Guid userId, CancellationToken cancellationToken)
    {
        var application = await LoadApplicationAsync(userId, track: false, cancellationToken);
        return await BuildResponseAsync(application, cancellationToken);
    }

    public async Task<FreelancerOnboardingResponse> UpdateOnboardingAsync(Guid userId, UpdateFreelancerOnboardingRequest request, int? expectedVersion, CancellationToken cancellationToken)
    {
        var application = await LoadApplicationAsync(userId, track: true, cancellationToken);
        EnsureEditable(application);
        EnsureVersion(application, expectedVersion);

        if (request.LegalFullName is not null) application.LegalFullName = request.LegalFullName.Trim();
        if (request.DateOfBirth is not null)
        {
            if (request.DateOfBirth > DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
                throw new ApiFlowException(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Ngày sinh không thể ở tương lai.");
            application.DateOfBirth = request.DateOfBirth;
        }
        if (request.Gender is not null) application.Gender = request.Gender;
        if (request.PermanentAddress is not null) application.PermanentAddress = request.PermanentAddress.Trim();
        if (request.CurrentAddress is not null) application.CurrentAddress = request.CurrentAddress.Trim();
        if (request.ExperienceYears is not null) application.ExperienceYears = request.ExperienceYears;
        if (request.CurrentStep is not null) application.CurrentStep = request.CurrentStep.Value;

        if (request.ServiceCategoryIds is not null)
        {
            await EnsureCatalogIdsAsync(request.ServiceCategoryIds, categories: true, cancellationToken);
            application.ServiceCategoryIdsJson = JsonSerializer.Serialize(request.ServiceCategoryIds);
        }
        if (request.ServiceAreaIds is not null)
        {
            await EnsureCatalogIdsAsync(request.ServiceAreaIds, categories: false, cancellationToken);
            application.ServiceAreaIdsJson = JsonSerializer.Serialize(request.ServiceAreaIds);
        }

        if (request.BankCode is not null) application.BankCode = request.BankCode.Trim();
        if (request.BankAccountNumber is not null)
        {
            var normalized = request.BankAccountNumber.Trim();
            if (normalized.Length is < 6 or > 64 || normalized.Any(c => !char.IsDigit(c)))
                throw new ApiFlowException(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Số tài khoản không hợp lệ.");
            application.BankAccountNumberEncrypted = dataProtector.Protect(normalized);
            application.BankAccountNumberLast4 = normalized[^4..];
        }
        if (request.BankAccountHolderName is not null)
        {
            var confirmedName = application.ConfirmedIdentityFullName;
            application.BankAccountHolderName = request.BankAccountHolderName.Trim();
            application.BankAccountHolderNameOverridden = confirmedName is not null &&
                !string.Equals(confirmedName, application.BankAccountHolderName, StringComparison.OrdinalIgnoreCase);
            if (application.BankAccountHolderNameOverridden && string.IsNullOrWhiteSpace(request.BankAccountHolderNameOverrideReason))
                throw new ApiFlowException(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Cần nêu lý do khi tên chủ tài khoản khác danh tính đã xác nhận.");
        }
        else if (application.BankAccountHolderName is null && application.ConfirmedIdentityFullName is { Length: > 0 })
        {
            application.BankAccountHolderName = application.ConfirmedIdentityFullName;
        }

        application.Version++;
        application.UpdatedAtUtc = timeProvider.GetUtcNow();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildResponseAsync(application, cancellationToken);
    }

    public async Task RecordConsentAsync(Guid userId, RecordKycConsentRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var application = await LoadApplicationAsync(userId, track: false, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var consentType = request.ConsentType ?? throw new ApiFlowException(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Thiếu loại consent.");

        if (!request.Accepted)
        {
            var active = await consentRepository.GetActiveAsync(userId, consentType, cancellationToken);
            if (active is not null)
            {
                dbContext.KycConsents.Attach(active);
                active.WithdrawnAtUtc = now;
                active.UpdatedAtUtc = now;
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        var consent = new KycConsent
        {
            UserId = userId,
            ApplicationId = application.Id,
            ConsentType = consentType,
            ContentVersion = request.ContentVersion,
            AcceptedAtUtc = now,
            IpAddress = ipAddress,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await consentRepository.AddAsync(consent, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<FreelancerDocumentResponse> UploadDocumentAsync(Guid userId, Stream content, string fileName, string? contentType, UploadOnboardingDocumentRequest metadata, CancellationToken cancellationToken)
    {
        var application = await LoadApplicationAsync(userId, track: true, cancellationToken);
        EnsureEditable(application);
        var type = metadata.Type ?? throw new ApiFlowException(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Thiếu loại tài liệu.");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        OnboardingDocumentPolicy.Validate(type, contentType, bytes.Length, _kyc.Upload, bytes);

        var now = timeProvider.GetUtcNow();
        buffer.Position = 0;
        var objectKey = await fileStorage.SaveAsync(buffer, contentType ?? "application/octet-stream", cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var previous = await documentRepository.GetCurrentAsync(application.Id, type, cancellationToken);
        var revision = 1;
        if (previous is not null)
        {
            dbContext.VerificationDocuments.Attach(previous);
            previous.SupersededAtUtc = now;
            previous.UpdatedAtUtc = now;
            revision = previous.Revision + 1;
        }

        var document = new VerificationDocument
        {
            ProviderId = application.ProviderId,
            ApplicationId = application.Id,
            Type = type,
            Revision = revision,
            ObjectKey = objectKey,
            OriginalFileName = fileName,
            ContentType = contentType,
            ContentHash = hash,
            ByteSize = bytes.Length,
            ScanStatus = DocumentScanStatus.Clean,
            Status = VerificationStatus.Pending,
            Issuer = metadata.Issuer,
            IssuedOn = metadata.IssuedOn,
            ExpiresOn = metadata.ExpiresOn,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await documentRepository.AddAsync(document, cancellationToken);

        await InvalidateDependentResultsAsync(application.Id, type, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new FreelancerDocumentResponse(document.Id, type.ToString(), revision, document.Status.ToString(),
            document.ScanStatus.ToString(), fileName, contentType, bytes.Length, now, true);
    }

    public async Task<DocumentContentResult?> GetDocumentContentAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var application = await LoadApplicationAsync(userId, track: false, cancellationToken);
        var document = await documentRepository.GetAsync(documentId, track: false, cancellationToken);
        if (document is null || document.ApplicationId != application.Id)
            return null;

        var stream = await fileStorage.OpenReadAsync(document.ObjectKey, cancellationToken);
        return stream is null
            ? null
            : new DocumentContentResult(stream, document.ContentType ?? "application/octet-stream", document.OriginalFileName);
    }

    public async Task DeleteDocumentAsync(Guid userId, Guid documentId, int? expectedVersion, CancellationToken cancellationToken)
    {
        var application = await LoadApplicationAsync(userId, track: true, cancellationToken);
        EnsureEditable(application);
        EnsureVersion(application, expectedVersion);

        var document = await documentRepository.GetAsync(documentId, track: true, cancellationToken);
        if (document is null || document.ApplicationId != application.Id)
            throw new ApiFlowException(StatusCodes.Status404NotFound, "DOCUMENT_NOT_FOUND", "Không tìm thấy tài liệu.");

        var now = timeProvider.GetUtcNow();
        document.SupersededAtUtc = now;
        document.UpdatedAtUtc = now;
        await InvalidateDependentResultsAsync(application.Id, document.Type, now, cancellationToken);

        application.Version++;
        application.UpdatedAtUtc = now;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<KycOperationResponse> StartOcrAsync(Guid userId, StartIdentityOcrRequest request, string? idempotencyKey, int? expectedVersion, CancellationToken cancellationToken)
    {
        var application = await LoadApplicationAsync(userId, track: true, cancellationToken);
        EnsureEditable(application);
        EnsureVersion(application, expectedVersion);
        await EnsureIdentityConsentAsync(userId, application.Id, cancellationToken);

        var front = await documentRepository.GetAsync(request.FrontDocumentId, track: false, cancellationToken);
        var back = await documentRepository.GetAsync(request.BackDocumentId, track: false, cancellationToken);
        if (front is null || front.ApplicationId != application.Id || front.Type != VerificationDocumentType.CitizenIdFront || front.SupersededAtUtc is not null)
            throw new ApiFlowException(StatusCodes.Status400BadRequest, "DOCUMENT_SIDE_INVALID", "Ảnh mặt trước CCCD không hợp lệ.");
        if (back is null || back.ApplicationId != application.Id || back.Type != VerificationDocumentType.CitizenIdBack || back.SupersededAtUtc is not null)
            throw new ApiFlowException(StatusCodes.Status400BadRequest, "DOCUMENT_SIDE_INVALID", "Ảnh mặt sau CCCD không hợp lệ.");

        var requestHash = ComputeRequestHash($"ocr|{front.Id}|{back.Id}|{application.Version}");
        return await EnqueueOperationAsync(userId, application, KycOperationType.IdentityOcr, idempotencyKey, requestHash, attempt: null,
            front.Id, back.Id, cancellationToken);
    }

    public async Task<FreelancerOnboardingResponse> ConfirmIdentityAsync(Guid userId, ConfirmIdentityRequest request, CancellationToken cancellationToken)
    {
        var application = await LoadApplicationAsync(userId, track: true, cancellationToken);
        EnsureEditable(application);

        var attempt = await attemptRepository.GetAsync(request.AttemptId, track: true, cancellationToken);
        if (attempt is null || attempt.ApplicationId != application.Id || attempt.SupersededAtUtc is not null)
            throw new ApiFlowException(StatusCodes.Status404NotFound, "ATTEMPT_NOT_FOUND", "Không tìm thấy phiên đọc CCCD hiện hành.");
        if (attempt.OcrState is not (OcrState.Extracted or OcrState.NeedsReview))
            throw new ApiFlowException(StatusCodes.Status409Conflict, "OCR_INCOMPLETE", "Dữ liệu CCCD chưa sẵn sàng để xác nhận.");

        var now = timeProvider.GetUtcNow();
        if (request.Corrections is { Count: > 0 })
        {
            attempt.CorrectionsJson = JsonSerializer.Serialize(request.Corrections);
            ApplyCorrection(attempt, request.Corrections, "documentNumber", value =>
            {
                attempt.DocumentNumberEncrypted = dataProtector.Protect(value);
                attempt.DocumentNumberLast4 = value.Length >= 4 ? value[^4..] : value;
                attempt.DocumentNumberFingerprint = fingerprintService.Compute(value);
            });
            ApplyCorrection(attempt, request.Corrections, "fullName", value => attempt.FullName = value);
            if (string.IsNullOrWhiteSpace(request.CorrectionReason))
                throw new ApiFlowException(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Cần nêu lý do khi đính chính dữ liệu OCR.");
        }

        attempt.ConfirmedAtUtc = now;
        attempt.UpdatedAtUtc = now;
        application.ConfirmedIdentityFullName = attempt.FullName;
        application.ConfirmedIdentityDocumentNumberLast4 = attempt.DocumentNumberLast4;
        if (application.BankAccountHolderName is null && attempt.FullName is { Length: > 0 })
            application.BankAccountHolderName = attempt.FullName;
        application.UpdatedAtUtc = now;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await BuildResponseAsync(application, cancellationToken);
    }

    public async Task<KycOperationResponse> StartFaceMatchAsync(Guid userId, StartFaceMatchRequest request, string? idempotencyKey, CancellationToken cancellationToken)
    {
        var application = await LoadApplicationAsync(userId, track: true, cancellationToken);
        EnsureEditable(application);
        await EnsureIdentityConsentAsync(userId, application.Id, cancellationToken);

        var attempt = await attemptRepository.GetAsync(request.AttemptId, track: true, cancellationToken);
        if (attempt is null || attempt.ApplicationId != application.Id || attempt.SupersededAtUtc is not null)
            throw new ApiFlowException(StatusCodes.Status404NotFound, "ATTEMPT_NOT_FOUND", "Không tìm thấy phiên đọc CCCD hiện hành.");
        if (attempt.OcrState is not (OcrState.Extracted or OcrState.NeedsReview))
            throw new ApiFlowException(StatusCodes.Status409Conflict, "OCR_INCOMPLETE", "Cần đọc CCCD trước khi so khớp khuôn mặt.");

        var selfie = await documentRepository.GetAsync(request.SelfieDocumentId, track: false, cancellationToken);
        if (selfie is null || selfie.ApplicationId != application.Id || selfie.Type != VerificationDocumentType.FaceVerification || selfie.SupersededAtUtc is not null)
            throw new ApiFlowException(StatusCodes.Status400BadRequest, "DOCUMENT_SIDE_INVALID", "Ảnh selfie không hợp lệ.");

        attempt.SelfieDocumentId = selfie.Id;
        attempt.UpdatedAtUtc = timeProvider.GetUtcNow();

        var requestHash = ComputeRequestHash($"face|{attempt.Id}|{selfie.Id}|{attempt.OcrState}");
        return await EnqueueOperationAsync(userId, application, KycOperationType.FaceMatch, idempotencyKey, requestHash, attempt,
            attempt.FrontDocumentId, attempt.BackDocumentId, cancellationToken);
    }

    public async Task<KycOperationResponse?> GetOperationAsync(Guid userId, Guid operationId, CancellationToken cancellationToken)
    {
        var operation = await operationRepository.GetAsync(operationId, track: false, cancellationToken);
        if (operation is null || operation.UserId != userId)
            return null;

        return ToOperationResponse(operation);
    }

    public async Task<FreelancerSubmissionSummaryResponse> SubmitAsync(Guid userId, SubmitApplicationRequest request, int? expectedVersion, CancellationToken cancellationToken)
    {
        var application = await LoadApplicationAsync(userId, track: true, cancellationToken);
        EnsureEditable(application);
        EnsureVersion(application, expectedVersion);

        var attempt = await attemptRepository.GetCurrentAsync(application.Id, track: false, cancellationToken);
        var documents = await documentRepository.ListCurrentByApplicationAsync(application.Id, cancellationToken);
        var missing = FreelancerOnboardingMapper.ComputeMissingFields(application, attempt, documents, _kyc.Policy);
        if (missing.Count > 0)
            throw new ApiFlowException(StatusCodes.Status422UnprocessableEntity, "APPLICATION_INCOMPLETE",
                $"Hồ sơ còn thiếu thông tin: {string.Join(", ", missing)}.");

        var identityConsent = await consentRepository.GetActiveAsync(userId, KycConsentType.IdentityProcessing, cancellationToken);
        if (identityConsent is null)
            throw new ApiFlowException(StatusCodes.Status409Conflict, "CONSENT_REQUIRED", "Thiếu đồng ý xử lý dữ liệu CCCD/sinh trắc học.");
        var finalConsent = await consentRepository.GetActiveAsync(userId, KycConsentType.FinalSubmission, cancellationToken);
        if (finalConsent is null || !string.Equals(finalConsent.ContentVersion, request.FinalConsentVersion, StringComparison.Ordinal))
            throw new ApiFlowException(StatusCodes.Status409Conflict, "CONSENT_REQUIRED", "Thiếu cam kết cuối cùng theo phiên bản nội dung hiện hành.");

        if (attempt!.DocumentNumberFingerprint is { Length: > 0 } fingerprint)
        {
            var existingClaim = await claimRepository.GetByFingerprintAsync(fingerprint, track: false, cancellationToken);
            if (existingClaim is not null && existingClaim.Status == IdentityClaimStatus.Reserved && existingClaim.ApplicationId != application.Id)
                throw new ApiFlowException(StatusCodes.Status409Conflict, "IDENTITY_CONFLICT", "Thông tin định danh đã được sử dụng cho một hồ sơ khác. Vui lòng liên hệ hỗ trợ.");
        }

        var now = timeProvider.GetUtcNow();
        var nextVersion = (application.SubmittedVersion ?? 0) + 1;
        var latest = await submissionRepository.GetLatestAsync(application.Id, cancellationToken);
        if (latest is not null && latest.Status == FreelancerApplicationStatus.Submitted && application.Status == FreelancerApplicationStatus.Submitted)
            return ToSubmissionSummary(latest);

        var snapshot = JsonSerializer.Serialize(new
        {
            application.Id,
            application.LegalFullName,
            application.DateOfBirth,
            Gender = application.Gender?.ToString(),
            application.PermanentAddress,
            application.CurrentAddress,
            application.ExperienceYears,
            ServiceCategoryIds = FreelancerOnboardingMapper.ReadIds(application.ServiceCategoryIdsJson),
            ServiceAreaIds = FreelancerOnboardingMapper.ReadIds(application.ServiceAreaIdsJson),
            application.BankCode,
            application.BankAccountNumberLast4,
            application.BankAccountHolderName,
            AttemptId = attempt.Id,
            attempt.DocumentNumberLast4,
            attempt.FullName,
            attempt.FaceScore,
            attempt.FaceThreshold,
            attempt.FaceState,
            attempt.OcrState,
            Documents = documents.Select(x => new { x.Id, Type = x.Type.ToString(), x.Revision, x.ContentHash, ScanStatus = x.ScanStatus.ToString() })
        });

        var submission = new ApplicationSubmission
        {
            ApplicationId = application.Id,
            ProviderId = application.ProviderId,
            Version = nextVersion,
            SnapshotJson = snapshot,
            Status = FreelancerApplicationStatus.Submitted,
            Decision = ProviderApprovalDecision.Pending,
            SubmittedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await submissionRepository.AddAsync(submission, cancellationToken);

        application.Version++;
        application.SubmittedVersion = nextVersion;
        application.SubmittedAtUtc = now;
        application.Status = FreelancerApplicationStatus.Submitted;
        application.CurrentStep = OnboardingStep.SkillsAndBank;
        application.LastDecisionReason = null;
        application.UpdatedAtUtc = now;

        var provider = await dbContext.Providers.SingleAsync(x => x.Id == application.ProviderId, cancellationToken);
        provider.ApprovalStatus = ApprovalStatus.Submitted;
        provider.UpdatedAtUtc = now;

        dbContext.ProviderApprovalReviews.Add(new ProviderApprovalReview
        {
            ProviderId = application.ProviderId,
            Decision = ProviderApprovalDecision.Pending,
            SubmittedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        if (attempt.DocumentNumberFingerprint is { Length: > 0 } fp)
        {
            var claim = await claimRepository.GetByFingerprintAsync(fp, track: true, cancellationToken);
            if (claim is null)
            {
                await claimRepository.AddAsync(new IdentityClaim
                {
                    Fingerprint = fp,
                    ApplicationId = application.Id,
                    UserId = userId,
                    Status = IdentityClaimStatus.Reserved,
                    ReservedAtUtc = now,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                }, cancellationToken);
            }
            else if (claim.Status == IdentityClaimStatus.Released && claim.ApplicationId == application.Id)
            {
                claim.Status = IdentityClaimStatus.Reserved;
                claim.ReleasedAtUtc = null;
                claim.UpdatedAtUtc = now;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToSubmissionSummary(submission);
    }

    public async Task<FreelancerOnboardingResponse> ReopenAsync(Guid userId, ReopenApplicationRequest request, CancellationToken cancellationToken)
    {
        var application = await LoadApplicationAsync(userId, track: true, cancellationToken);
        if (application.Status is not (FreelancerApplicationStatus.Rejected or FreelancerApplicationStatus.ChangesRequested))
            throw new ApiFlowException(StatusCodes.Status409Conflict, "APPLICATION_LOCKED", "Hồ sơ hiện tại không thể mở lại để chỉnh sửa.");

        var now = timeProvider.GetUtcNow();
        application.Status = FreelancerApplicationStatus.Draft;
        application.Version++;
        application.LastDecisionReason = null;
        application.UpdatedAtUtc = now;

        var provider = await dbContext.Providers.SingleAsync(x => x.Id == application.ProviderId, cancellationToken);
        provider.ApprovalStatus = ApprovalStatus.Draft;
        provider.UpdatedAtUtc = now;

        var claim = await dbContext.IdentityClaims
            .SingleOrDefaultAsync(x => x.ApplicationId == application.Id && x.Status == IdentityClaimStatus.Reserved, cancellationToken);
        if (claim is not null)
        {
            claim.Status = IdentityClaimStatus.Released;
            claim.ReleasedAtUtc = now;
            claim.UpdatedAtUtc = now;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildResponseAsync(application, cancellationToken);
    }

    private async Task<KycOperationResponse> EnqueueOperationAsync(
        Guid userId,
        FreelancerApplication application,
        KycOperationType type,
        string? idempotencyKey,
        string requestHash,
        IdentityVerificationAttempt? attempt,
        Guid? frontId,
        Guid? backId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ApiFlowException(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Thiếu header Idempotency-Key.");

        var existing = await operationRepository.FindByIdempotencyKeyAsync(userId, type, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                throw new ApiFlowException(StatusCodes.Status409Conflict, "IDEMPOTENCY_KEY_REUSED", "Idempotency-Key đã dùng cho yêu cầu khác.");
            return ToOperationResponse(existing);
        }

        var now = timeProvider.GetUtcNow();
        IdentityVerificationAttempt target;
        if (type == KycOperationType.IdentityOcr)
        {
            await SupersedeAttemptsAsync(application.Id, now, cancellationToken);
            target = new IdentityVerificationAttempt
            {
                ApplicationId = application.Id,
                ApplicationVersion = application.Version,
                FrontDocumentId = frontId,
                BackDocumentId = backId,
                OcrState = OcrState.Processing,
                FaceState = FaceState.NotStarted,
                LivenessState = LivenessState.NotPerformed,
                PolicyVersion = _kyc.Policy.Version,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            await attemptRepository.AddAsync(target, cancellationToken);
        }
        else
        {
            target = attempt!;
            target.FaceState = FaceState.Processing;
            target.UpdatedAtUtc = now;
        }

        var operation = new KycOperation
        {
            UserId = userId,
            ApplicationId = application.Id,
            Type = type,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            State = KycOperationState.Queued,
            AttemptId = target.Id,
            InputRevisionId = target.Id,
            NextAttemptAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await operationRepository.AddAsync(operation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToOperationResponse(operation);
    }

    private async Task InvalidateDependentResultsAsync(Guid applicationId, VerificationDocumentType type, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (type is VerificationDocumentType.CitizenIdFront or VerificationDocumentType.CitizenIdBack)
        {
            await SupersedeAttemptsAsync(applicationId, now, cancellationToken);
            return;
        }

        if (type == VerificationDocumentType.FaceVerification)
        {
            var attempt = await attemptRepository.GetCurrentAsync(applicationId, track: true, cancellationToken);
            if (attempt is not null && attempt.FaceState != FaceState.NotStarted)
            {
                attempt.FaceState = FaceState.NotStarted;
                attempt.SelfieDocumentId = null;
                attempt.FaceScore = null;
                attempt.FaceThreshold = null;
                attempt.FaceProviderRequestId = null;
                attempt.ResultCode = null;
                attempt.UpdatedAtUtc = now;
            }
        }
    }

    private async Task SupersedeAttemptsAsync(Guid applicationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var attempts = await dbContext.IdentityVerificationAttempts
            .Where(x => x.ApplicationId == applicationId && x.SupersededAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var attempt in attempts)
        {
            attempt.SupersededAtUtc = now;
            attempt.UpdatedAtUtc = now;
        }

        var operations = await dbContext.KycOperations
            .Where(x => x.ApplicationId == applicationId && (x.State == KycOperationState.Queued || x.State == KycOperationState.Processing))
            .ToListAsync(cancellationToken);
        foreach (var operation in operations)
        {
            operation.State = KycOperationState.Superseded;
            operation.UpdatedAtUtc = now;
        }
    }

    private async Task EnsureIdentityConsentAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken)
    {
        var consent = await consentRepository.GetActiveAsync(userId, KycConsentType.IdentityProcessing, cancellationToken);
        if (consent is null)
            throw new ApiFlowException(StatusCodes.Status409Conflict, "CONSENT_REQUIRED", "Cần đồng ý xử lý dữ liệu CCCD/sinh trắc học trước khi gửi cho nhà cung cấp.");
    }

    private async Task EnsureCatalogIdsAsync(IReadOnlyList<Guid> ids, bool categories, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            throw new ApiFlowException(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Cần chọn ít nhất một mục.");
        var distinct = ids.Distinct().ToList();
        var count = categories
            ? await dbContext.ServiceCategories.CountAsync(x => distinct.Contains(x.Id) && x.IsActive, cancellationToken)
            : await dbContext.ServiceAreas.CountAsync(x => distinct.Contains(x.Id) && x.IsActive, cancellationToken);
        if (count != distinct.Count)
            throw new ApiFlowException(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Danh mục không tồn tại hoặc đã ngừng hoạt động.");
    }

    private async Task<FreelancerApplication> LoadApplicationAsync(Guid userId, bool track, CancellationToken cancellationToken)
    {
        var application = await applicationRepository.GetByUserIdAsync(userId, track, cancellationToken);
        if (application is null)
            throw new ApiFlowException(StatusCodes.Status404NotFound, "APPLICATION_NOT_FOUND", "Không tìm thấy hồ sơ freelancer.");
        return application;
    }

    private async Task<FreelancerOnboardingResponse> BuildResponseAsync(FreelancerApplication application, CancellationToken cancellationToken)
    {
        var attempt = await attemptRepository.GetCurrentAsync(application.Id, track: false, cancellationToken);
        var documents = await documentRepository.ListByApplicationAsync(application.Id, track: false, cancellationToken);
        var latest = await submissionRepository.GetLatestAsync(application.Id, cancellationToken);
        return FreelancerOnboardingMapper.ToResponse(application, attempt, documents, latest, _kyc.Policy);
    }

    private void EnsureEditable(FreelancerApplication application)
    {
        if (!EditableStatuses.Contains(application.Status))
            throw new ApiFlowException(StatusCodes.Status409Conflict, "APPLICATION_LOCKED", "Hồ sơ đã được gửi và đang chờ thẩm định.");
    }

    private void EnsureVersion(FreelancerApplication application, int? expectedVersion)
    {
        if (expectedVersion is not null && expectedVersion.Value != application.Version)
            throw new ApiFlowException(StatusCodes.Status409Conflict, "STALE_APPLICATION_VERSION", "Hồ sơ đã thay đổi ở phiên bản khác.");
    }

    private static void ApplyCorrection(IdentityVerificationAttempt attempt, IReadOnlyDictionary<string, string?> corrections, string field, Action<string> apply)
    {
        if (corrections.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value))
            apply(value.Trim());
    }

    private KycOperationResponse ToOperationResponse(KycOperation operation)
    {
        var nextPoll = operation.State is KycOperationState.Queued or KycOperationState.Processing ? 2 : 0;
        return new KycOperationResponse(operation.Id, operation.Type.ToString(), operation.State.ToString(),
            operation.ErrorCode, nextPoll, operation.CreatedAtUtc, operation.CompletedAtUtc);
    }

    private static FreelancerSubmissionSummaryResponse ToSubmissionSummary(ApplicationSubmission submission) =>
        new(submission.Id, submission.Version, submission.Status.ToString(), submission.Decision.ToString(),
            submission.SubmittedAtUtc, submission.DecidedAtUtc, submission.DecisionReason,
            FreelancerOnboardingMapper.ApplicationCode(submission));

    private static string ComputeRequestHash(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
}
