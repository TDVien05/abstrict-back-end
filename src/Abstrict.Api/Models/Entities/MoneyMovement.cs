using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class MoneyMovement : Entity
{
    public Guid? BookingId { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? BeneficiaryProviderId { get; set; }
    public Guid? ProviderWalletId { get; set; }
    public MoneyMovementType Type { get; set; }
    public MoneyMovementStatus Status { get; set; } = MoneyMovementStatus.Pending;
    public long AmountVnd { get; set; }
    public required string Reference { get; set; }
    public DateTimeOffset? PostedAtUtc { get; set; }
    public string? Note { get; set; }
    public Booking? Booking { get; set; }
    public Payment? Payment { get; set; }
    public Provider? BeneficiaryProvider { get; set; }
    public ProviderWallet? ProviderWallet { get; set; }
}
