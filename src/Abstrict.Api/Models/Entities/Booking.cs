using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class Booking : Entity
{
    public required string BookingNumber { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid ProviderId { get; set; }
    public Guid ServiceCategoryId { get; set; }
    public Guid? CustomerAddressId { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.PendingDeposit;
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset EndsAtUtc { get; set; }
    public DateTimeOffset? HoldExpiresAtUtc { get; set; }
    public DateTimeOffset? ProviderConfirmationDeadlineUtc { get; set; }
    public DateTimeOffset? CheckedInAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public long UnitPriceVnd { get; set; }
    public long BaseAmountVnd { get; set; }
    public long SurchargeAmountVnd { get; set; }
    public long DepositAmountVnd { get; set; }
    public long ExtensionAmountVnd { get; set; }
    public required string AddressSnapshot { get; set; }
    public string? CustomerNote { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public string? CancellationReason { get; set; }
    public User CustomerUser { get; set; } = null!;
    public Provider Provider { get; set; } = null!;
    public ServiceCategory ServiceCategory { get; set; } = null!;
    public CustomerAddress? CustomerAddress { get; set; }
    public ICollection<BookingTask> Tasks { get; set; } = new List<BookingTask>();
    public ICollection<BookingStatusHistory> StatusHistory { get; set; } = new List<BookingStatusHistory>();
    public ICollection<BookingPhoto> Photos { get; set; } = new List<BookingPhoto>();
    public ICollection<BookingExtension> Extensions { get; set; } = new List<BookingExtension>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
