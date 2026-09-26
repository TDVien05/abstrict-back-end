using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class Dispute : Entity
{
    public Guid BookingId { get; set; }
    public Guid OpenedByUserId { get; set; }
    public DisputeCategory Category { get; set; }
    public DisputeStatus Status { get; set; } = DisputeStatus.Open;
    public required string Description { get; set; }
    public DateTimeOffset ResponseDeadlineUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public DateTimeOffset? AppealDeadlineUtc { get; set; }
    public bool AppealUsed { get; set; }
    public Booking Booking { get; set; } = null!;
    public ICollection<DisputeEvidence> Evidence { get; set; } = new List<DisputeEvidence>();
    public ICollection<DisputeDecision> Decisions { get; set; } = new List<DisputeDecision>();
    public ICollection<DisputeMessage> Messages { get; set; } = new List<DisputeMessage>();
}
