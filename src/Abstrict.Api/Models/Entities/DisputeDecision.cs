using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class DisputeDecision : Entity
{
    public Guid DisputeId { get; set; }
    public Guid AdminUserId { get; set; }
    public DisputeDecisionType Type { get; set; }
    public required string OutcomeCode { get; set; }
    public required string Reason { get; set; }
    public long? RefundAmountVnd { get; set; }
    public long? ProviderAmountVnd { get; set; }
    public DateTimeOffset DecidedAtUtc { get; set; }
    public Dispute Dispute { get; set; } = null!;
}
