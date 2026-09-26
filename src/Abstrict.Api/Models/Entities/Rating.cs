using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class Rating : Entity
{
    public Guid BookingId { get; set; }
    public Guid ReviewerUserId { get; set; }
    public RatingSubjectType SubjectType { get; set; }
    public Guid? SubjectUserId { get; set; }
    public Guid? SubjectProviderId { get; set; }
    public int Score { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset PublishAfterUtc { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public Booking Booking { get; set; } = null!;
}
