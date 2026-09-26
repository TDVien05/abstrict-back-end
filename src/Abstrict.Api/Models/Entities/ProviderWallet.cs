using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class ProviderWallet : Entity
{
    public Guid ProviderId { get; set; }
    public ProviderWalletType Type { get; set; }
    public ProviderWalletStatus Status { get; set; } = ProviderWalletStatus.Active;
    public string Currency { get; set; } = "VND";
    public Provider Provider { get; set; } = null!;
    public ICollection<MoneyMovement> MoneyMovements { get; set; } = new List<MoneyMovement>();
}
