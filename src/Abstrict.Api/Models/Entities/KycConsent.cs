using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class KycConsent : Entity
{
    public Guid UserId { get; set; }
    public Guid? ApplicationId { get; set; }
    public KycConsentType ConsentType { get; set; }
    public required string ContentVersion { get; set; }
    public DateTimeOffset AcceptedAtUtc { get; set; }
    public DateTimeOffset? WithdrawnAtUtc { get; set; }
    public string? IpAddress { get; set; }

    public User User { get; set; } = null!;
}
