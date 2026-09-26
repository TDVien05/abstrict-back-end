namespace Abstrict.Api.Models.Entities;

public sealed class CheckInToken : Entity
{
    public Guid BookingId { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RedeemedAtUtc { get; set; }
    public Guid? RedeemedByUserId { get; set; }
    public Guid? RedeemedByCompanyWorkerId { get; set; }
    public Booking Booking { get; set; } = null!;
}
