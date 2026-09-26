namespace Abstrict.Api.Models.Entities;

public sealed class Notification : Entity
{
    public Guid UserId { get; set; }
    public required string Type { get; set; }
    public required string Title { get; set; }
    public required string Body { get; set; }
    public string? DataJson { get; set; }
    public DateTimeOffset? ReadAtUtc { get; set; }
    public User User { get; set; } = null!;
}
