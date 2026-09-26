using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class BookingTask : Entity
{
    public Guid BookingId { get; set; }
    public Guid? TemplateItemId { get; set; }
    public required string AreaNameSnapshot { get; set; }
    public required string DescriptionSnapshot { get; set; }
    public int SortOrder { get; set; }
    public BookingTaskStatus Status { get; set; } = BookingTaskStatus.Pending;
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public Booking Booking { get; set; } = null!;
}
