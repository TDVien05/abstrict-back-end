using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class PromotionPurchase : Entity
{
    public Guid ProviderId { get; set; }
    public Guid PromotionPlanId { get; set; }
    public Guid? PaymentId { get; set; }
    public PromotionPurchaseStatus Status { get; set; } = PromotionPurchaseStatus.PendingPayment;
    public long PriceSnapshotVnd { get; set; }
    public DateTimeOffset? StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public Provider Provider { get; set; } = null!;
    public PromotionPlan PromotionPlan { get; set; } = null!;
    public Payment? Payment { get; set; }
}
