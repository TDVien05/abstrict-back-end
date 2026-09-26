namespace Abstrict.Api.Models.Entities;

public sealed class ProviderServiceArea : Entity
{
    public Guid ProviderId { get; set; }
    public Guid ServiceAreaId { get; set; }
    public int? TravelRadiusKm { get; set; }
    public Provider Provider { get; set; } = null!;
    public ServiceArea ServiceArea { get; set; } = null!;
}
