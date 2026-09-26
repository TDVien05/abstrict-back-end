using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class BookingPhoto : Entity
{
    public Guid BookingId { get; set; }
    public Guid? BookingTaskId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public BookingPhotoType Type { get; set; }
    public required string ObjectKey { get; set; }
    public string? Caption { get; set; }
    public DateTimeOffset TakenAtUtc { get; set; }
    public Booking Booking { get; set; } = null!;
}
