namespace Abstrict.Api.Models.Entities;

public sealed class ProviderService : Entity
{
    public Guid ProviderId { get; set; }
    public Guid ServiceCategoryId { get; set; }
    public long HourlyRateVnd { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public Provider Provider { get; set; } = null!;
    public ServiceCategory ServiceCategory { get; set; } = null!;
}
