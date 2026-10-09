namespace Abstrict.Api.DTOs.Responses;

public sealed record KycDocumentResponse(
    Guid Id,
    string Type,
    string Status,
    string OriginalFileName,
    DateTimeOffset UploadedAtUtc,
    string? RejectionReason);

public sealed record KycProfileResponse(
    string LegalFullName,
    DateOnly DateOfBirth,
    string Gender,
    string CitizenIdLast4,
    string PermanentAddress,
    string CurrentAddress,
    int ExperienceYears);

public sealed record KycFeedbackResponse(string Decision, string? Reason, DateTimeOffset DecidedAtUtc);

public sealed record FreelancerKycResponse(
    // NotStarted, Draft, Submitted, UnderReview, Approved, Rejected
    string Status,
    bool CanEdit,
    KycProfileResponse? Profile,
    IReadOnlyCollection<KycDocumentResponse> Documents,
    KycFeedbackResponse? Feedback,
    DateTimeOffset? SubmittedAtUtc);

public sealed record AdminKycListItemResponse(
    Guid ProviderId,
    string LegalFullName,
    string PhoneNumber,
    string Status,
    DateTimeOffset? SubmittedAtUtc);

public sealed record AdminKycDetailResponse(
    Guid ProviderId,
    string Status,
    string PhoneNumber,
    string LegalFullName,
    DateOnly DateOfBirth,
    string Gender,
    string CitizenIdNumber,
    string PermanentAddress,
    string CurrentAddress,
    int ExperienceYears,
    DateTimeOffset? SubmittedAtUtc,
    KycFeedbackResponse? Feedback,
    IReadOnlyCollection<KycDocumentResponse> Documents);
