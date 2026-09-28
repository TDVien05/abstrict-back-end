using System.Text.Json.Serialization;

namespace Abstrict.Api.DTOs.Responses;

public sealed record FreelancerRegistrationResponse(
    Guid UserId,
    Guid ChallengeId,
    string PhoneNumber,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset ResendAvailableAtUtc)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DevelopmentOtpCode { get; init; }
}

public sealed record FreelancerPhoneVerificationResponse(
    Guid UserId,
    string Status,
    DateTimeOffset PhoneVerifiedAtUtc);

public sealed record FreelancerLoginResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAtUtc,
    Guid UserId,
    string FullName,
    string PhoneNumber,
    string Role,
    string OnboardingStatus,
    string CurrentStep);
