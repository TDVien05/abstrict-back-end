namespace Abstrict.Api.Models.Entities;

public sealed class BankAccount : Entity
{
    public Guid ProviderId { get; set; }
    public required string BankCode { get; set; }
    public required string AccountNumberEncrypted { get; set; }
    public required string AccountHolderName { get; set; }
    public bool IsVerified { get; set; }
    public bool IsDefault { get; set; }
    public Provider Provider { get; set; } = null!;
}
