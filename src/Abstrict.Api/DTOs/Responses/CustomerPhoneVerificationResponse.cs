namespace Abstrict.Api.DTOs.Responses;

public sealed record CustomerPhoneVerificationResponse(Guid UserId, string Status, DateTimeOffset PhoneVerifiedAtUtc);
