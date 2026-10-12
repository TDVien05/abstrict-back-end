namespace Abstrict.Api.DTOs.Responses;

/// <summary>
/// <c>Role</c> là Customer, Freelancer, Company hoặc Admin. <c>CustomerProfile</c> chỉ có với khách hàng,
/// <c>FreelancerOnboarding</c> chỉ có với freelancer, <c>CompanyOnboarding</c> chỉ có với công ty; các vai trò khác nhận <c>null</c>.
/// </summary>
public sealed record CustomerLoginResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAtUtc,
    Guid UserId,
    string FullName,
    string PhoneNumber,
    string Role,
    CustomerProfileResponse? CustomerProfile,
    FreelancerOnboardingResponse? FreelancerOnboarding,
    CompanyOnboardingSummaryResponse? CompanyOnboarding);

public enum FreelancerOnboardingStep { Profile, Documents, Submit, AwaitingReview, Completed, Suspended }

/// <summary>
/// Tiến độ KYC để điều hướng freelancer sau khi đăng nhập. <c>KycStatus</c> là NotStarted, Draft, Submitted,
/// UnderReview, Approved, Rejected hoặc Suspended; <c>CurrentStep</c> là bước tiếp theo cần làm.
/// </summary>
public sealed record FreelancerOnboardingResponse(Guid? ProviderId, string KycStatus, FreelancerOnboardingStep CurrentStep);
