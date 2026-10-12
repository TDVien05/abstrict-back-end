using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.DTOs.Responses;

public enum CompanyOnboardingStep { Company, Representative, ServiceAreas, Documents, Submit, AwaitingReview, Completed, Suspended }

/// <summary>
/// Tiến độ đăng ký của công ty, trả kèm khi đăng nhập. <c>Status</c> là NotStarted hoặc tên trạng thái duyệt
/// (Draft, Submitted, UnderReview, Approved, Rejected, Suspended); <c>CurrentStep</c> là bước tiếp theo cần làm.
/// </summary>
public sealed record CompanyOnboardingSummaryResponse(Guid? ProviderId, string Status, CompanyOnboardingStep CurrentStep);

public sealed record CompanyProfileDetailResponse(
    string LegalName,
    string DisplayName,
    string TaxCode,
    string Hotline,
    string HeadquartersAddress,
    CompanyWorkforceSize WorkforceSize,
    long? LiabilityCoverageVnd);

/// <summary>Chỉ có 4 số cuối CCCD; số đầy đủ chỉ admin duyệt hồ sơ mới xem được.</summary>
public sealed record CompanyRepresentativeResponse(
    string FullName,
    string JobTitle,
    string WorkEmail,
    string PhoneNumber,
    string IdentityNumberLast4);

public sealed record CompanyServiceAreaResponse(Guid Id, string City, string District, string? WardOrComplex);

public sealed record CompanyDocumentResponse(
    Guid Id,
    VerificationDocumentType Type,
    VerificationStatus Status,
    string OriginalFileName,
    string ContentType,
    DateTimeOffset UploadedAtUtc,
    string? RejectionReason);

public sealed record CompanyOnboardingResponse(
    string Status,
    CompanyOnboardingStep CurrentStep,
    bool CanEdit,
    Guid? ProviderId,
    CompanyProfileDetailResponse? Company,
    CompanyRepresentativeResponse? Representative,
    IReadOnlyCollection<CompanyServiceAreaResponse> ServiceAreas,
    IReadOnlyCollection<VerificationDocumentType> RequiredDocumentTypes,
    IReadOnlyCollection<CompanyDocumentResponse> Documents,
    KycFeedbackResponse? Feedback,
    DateTimeOffset? SubmittedAtUtc);

public sealed record AdminCompanyListItemResponse(
    Guid ProviderId,
    string LegalName,
    string TaxCode,
    string OwnerPhoneNumber,
    string Status,
    DateTimeOffset? SubmittedAtUtc);

public sealed record AdminCompanyRepresentativeResponse(
    string FullName,
    string JobTitle,
    string WorkEmail,
    string PhoneNumber,
    string IdentityNumber);

public sealed record AdminCompanyDetailResponse(
    Guid ProviderId,
    string Status,
    string OwnerPhoneNumber,
    string OwnerFullName,
    CompanyProfileDetailResponse Company,
    AdminCompanyRepresentativeResponse? Representative,
    IReadOnlyCollection<CompanyServiceAreaResponse> ServiceAreas,
    IReadOnlyCollection<VerificationDocumentType> RequiredDocumentTypes,
    IReadOnlyCollection<CompanyDocumentResponse> Documents,
    KycFeedbackResponse? Feedback,
    DateTimeOffset? SubmittedAtUtc);
