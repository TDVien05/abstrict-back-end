using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class ProviderAssessment : Entity
{
    public Guid ProviderId { get; set; }
    public ProviderAssessmentType Type { get; set; }
    public int Score { get; set; }
    public int MaximumScore { get; set; } = 100;
    public int PassingScore { get; set; }
    public bool Passed { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public string? ResultJson { get; set; }
    public Provider Provider { get; set; } = null!;
}
