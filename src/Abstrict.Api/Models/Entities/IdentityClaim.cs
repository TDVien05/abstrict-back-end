using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class IdentityClaim : Entity
{
    public required string Fingerprint { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid UserId { get; set; }
    public IdentityClaimStatus Status { get; set; } = IdentityClaimStatus.Reserved;
    public DateTimeOffset ReservedAtUtc { get; set; }
    public DateTimeOffset? ReleasedAtUtc { get; set; }

    public FreelancerApplication Application { get; set; } = null!;
}
