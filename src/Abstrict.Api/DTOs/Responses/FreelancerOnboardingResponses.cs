namespace Abstrict.Api.DTOs.Responses;

public sealed record FreelancerPersonalInfoResponse(
    string? LegalFullName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? PermanentAddress,
    string? CurrentAddress,
    int? ExperienceYears);

public sealed record FreelancerBankInfoResponse(
    string? BankCode,
    string? AccountNumberMasked,
    string? AccountHolderName,
    bool AccountHolderNameOverridden,
    bool IsVerified);

public sealed record FreelancerSkillsAndBankResponse(
    IReadOnlyList<Guid> ServiceCategoryIds,
    IReadOnlyList<Guid> ServiceAreaIds,
    FreelancerBankInfoResponse Bank);

public sealed record FreelancerIdentityResponse(
    Guid? AttemptId,
    Guid? FrontDocumentId,
    Guid? BackDocumentId,
    Guid? SelfieDocumentId,
    string OcrState,
    string FaceState,
    string LivenessState,
    string? DocumentNumberMasked,
    string? FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? PermanentAddress,
    DateOnly? IssuedOn,
    string? IssuedPlace,
    DateOnly? ExpiresOn,
    decimal? FaceScore,
    decimal? FaceThreshold,
    string? ResultCode,
    DateTimeOffset? ConfirmedAtUtc,
    bool ConfirmationRequired);

public sealed record FreelancerDocumentResponse(
    Guid DocumentId,
    string Type,
    int Revision,
    string Status,
    string ScanStatus,
    string OriginalFileName,
    string? ContentType,
    long? ByteSize,
    DateTimeOffset UploadedAtUtc,
    bool IsCurrent);

public sealed record FreelancerSubmissionSummaryResponse(
    Guid SubmissionId,
    int Version,
    string Status,
    string Decision,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    string? DecisionReason,
    string? ApplicationCode);

public sealed record FreelancerOnboardingRequirementsResponse(
    string PolicyVersion,
    bool RequireHealthCertificate,
    bool RequireCriminalRecord,
    bool AllowManualReview,
    int MaxFaceAttemptsPerDay,
    string LivenessMode);

public sealed record FreelancerOnboardingResponse(
    Guid ApplicationId,
    int Version,
    string Status,
    string CurrentStep,
    FreelancerPersonalInfoResponse Personal,
    FreelancerIdentityResponse Identity,
    IReadOnlyList<FreelancerDocumentResponse> Documents,
    FreelancerSkillsAndBankResponse SkillsAndBank,
    IReadOnlyList<string> MissingFields,
    FreelancerOnboardingRequirementsResponse Requirements,
    FreelancerSubmissionSummaryResponse? LatestSubmission,
    string? LastDecisionReason);

public sealed record KycOperationResponse(
    Guid OperationId,
    string Type,
    string State,
    string? ErrorCode,
    int NextPollAfterSeconds,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc);
