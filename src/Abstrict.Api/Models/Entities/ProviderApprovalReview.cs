using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class ProviderApprovalReview : Entity
{
    public Guid ProviderId { get; set; }
    public Guid? AssignedAdminUserId { get; set; }
    public Guid? DecidedByAdminUserId { get; set; }
    public ProviderApprovalDecision Decision { get; set; } = ProviderApprovalDecision.Pending;
    public DateTimeOffset SubmittedAtUtc { get; set; }
    public DateTimeOffset? ReviewStartedAtUtc { get; set; }
    public DateTimeOffset? DecidedAtUtc { get; set; }
    public string? DecisionReason { get; set; }
    public string? RequestedChangesJson { get; set; }
    public Provider Provider { get; set; } = null!;
}
