namespace Abstrict.Api.Models.Entities;

public sealed class CustomerProfile : Entity
{
    public Guid UserId { get; set; }
    public required string FullName { get; set; }
    public string? AvatarObjectKey { get; set; }
    public User User { get; set; } = null!;
}
