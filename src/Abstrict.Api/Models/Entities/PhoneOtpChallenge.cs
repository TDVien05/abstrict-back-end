using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class PhoneOtpChallenge : Entity
{
    public Guid UserId { get; set; }
    public required string PhoneNumber { get; set; }
    public required string CodeHash { get; set; }
    public OtpPurpose Purpose { get; set; }
    public OtpDeliveryChannel DeliveryChannel { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset ResendAvailableAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
    public DateTimeOffset? LockedAtUtc { get; set; }
    public int FailedAttemptCount { get; set; }
    public User User { get; set; } = null!;
}
