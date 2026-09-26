using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class Payment : Entity
{
    public Guid? BookingId { get; set; }
    public Guid PayerUserId { get; set; }
    public PaymentPurpose Purpose { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public long AmountVnd { get; set; }
    public required string IdempotencyKey { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? ProviderName { get; set; }
    public DateTimeOffset? PaidAtUtc { get; set; }
    public DateTimeOffset? RefundedAtUtc { get; set; }
    public Booking? Booking { get; set; }
    public User PayerUser { get; set; } = null!;
    public ICollection<MoneyMovement> MoneyMovements { get; set; } = new List<MoneyMovement>();
}
