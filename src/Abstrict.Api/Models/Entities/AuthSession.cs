namespace Abstrict.Api.Models.Entities;

public sealed class AuthSession : Entity
{
    public Guid UserId { get; set; }
    public required string RefreshTokenHash { get; set; }
    public bool IsPersistent { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset LastSeenAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public User User { get; set; } = null!;
}
