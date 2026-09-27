namespace Abstrict.Api.DTOs.Responses;

public sealed record CustomerLoginResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAtUtc,
    Guid UserId,
    string FullName,
    string PhoneNumber,
    string Role);
