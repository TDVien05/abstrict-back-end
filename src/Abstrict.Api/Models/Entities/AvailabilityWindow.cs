namespace Abstrict.Api.Models.Entities;

public sealed class AvailabilityWindow : Entity
{
    public Guid ProviderId { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset EndsAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public Provider Provider { get; set; } = null!;
}
