using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class BookingStatusHistory : Entity
{
    public Guid BookingId { get; set; }
    public BookingStatus FromStatus { get; set; }
    public BookingStatus ToStatus { get; set; }
    public Guid? ChangedByUserId { get; set; }
    /// <summary>Mã lý do ổn định, xem <c>BookingTimelineReasons</c>.</summary>
    public string? Reason { get; set; }
    /// <summary>Ghi chú tự do do người dùng nhập (lý do hủy/từ chối).</summary>
    public string? Note { get; set; }
    public Booking Booking { get; set; } = null!;
}
