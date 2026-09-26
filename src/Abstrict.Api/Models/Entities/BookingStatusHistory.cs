using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class BookingStatusHistory : Entity
{
    public Guid BookingId { get; set; }
    public BookingStatus FromStatus { get; set; }
    public BookingStatus ToStatus { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string? Reason { get; set; }
    public Booking Booking { get; set; } = null!;
}
