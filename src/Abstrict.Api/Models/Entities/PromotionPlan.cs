using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class PromotionPlan : Entity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public PromotionAudience Audience { get; set; }
    public int DurationDays { get; set; }
    public long PriceVnd { get; set; }
    public int? PriorityRadiusKm { get; set; }
    public int? WorkerLimit { get; set; }
    public string? BenefitsJson { get; set; }
    public bool IsActive { get; set; }
}
