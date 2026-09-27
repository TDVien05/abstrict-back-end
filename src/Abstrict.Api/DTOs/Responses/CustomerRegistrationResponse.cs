using System.Text.Json.Serialization;

namespace Abstrict.Api.DTOs.Responses;

public sealed record CustomerRegistrationResponse(
    Guid UserId,
    Guid ChallengeId,
    string PhoneNumber,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset ResendAvailableAtUtc)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DevelopmentOtpCode { get; init; }
}
