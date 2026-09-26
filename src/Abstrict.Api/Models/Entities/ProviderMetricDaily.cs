namespace Abstrict.Api.Models.Entities;

public sealed class ProviderMetricDaily : Entity
{
    public Guid ProviderId { get; set; }
    public DateOnly MetricDate { get; set; }
    public int ImpressionCount { get; set; }
    public int ProfileViewCount { get; set; }
    public int BookingConversionCount { get; set; }
    public Provider Provider { get; set; } = null!;
}
