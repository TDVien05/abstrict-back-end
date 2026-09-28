namespace Abstrict.Api.DTOs.Responses;

public sealed record CatalogServiceAreaResponse(
    Guid Id,
    string City,
    string District,
    string? WardOrComplex,
    bool IsActive);

public sealed record CatalogServiceCategoryResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive);

public sealed record CatalogBankResponse(
    string Code,
    string Name);

public sealed record AdminFreelancerApplicationSummaryResponse(
    Guid ApplicationId,
    Guid SubmissionId,
    int SubmissionVersion,
    string Status,
    string Decision,
    Guid UserId,
    string PhoneNumber,
    string? LegalFullName,
    string? DocumentNumberMasked,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset? ReviewStartedAtUtc,
    DateTimeOffset? DecidedAtUtc);

public sealed record AdminFreelancerApplicationDetailResponse(
    Guid ApplicationId,
    Guid ProviderId,
    int Version,
    string Status,
    string CurrentStep,
    Guid UserId,
    string PhoneNumber,
    FreelancerPersonalInfoResponse Personal,
    FreelancerIdentityResponse Identity,
    IReadOnlyList<FreelancerDocumentResponse> Documents,
    FreelancerSkillsAndBankResponse SkillsAndBank,
    FreelancerSubmissionSummaryResponse LatestSubmission,
    string? LastDecisionReason);
