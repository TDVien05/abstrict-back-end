using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class BookingExtension : Entity
{
    public Guid BookingId { get; set; }
    public Guid ProposedByUserId { get; set; }
    public BookingExtensionStatus Status { get; set; } = BookingExtensionStatus.Proposed;
    public int AdditionalMinutes { get; set; }
    public long AdditionalAmountVnd { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? CustomerAcceptedAtUtc { get; set; }
    public DateTimeOffset? ProviderAcceptedAtUtc { get; set; }
    public Booking Booking { get; set; } = null!;
}
