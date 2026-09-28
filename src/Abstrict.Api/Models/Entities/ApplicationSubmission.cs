using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class ApplicationSubmission : Entity
{
    public Guid ApplicationId { get; set; }
    public Guid ProviderId { get; set; }
    public int Version { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    public FreelancerApplicationStatus Status { get; set; } = FreelancerApplicationStatus.Submitted;
    public ProviderApprovalDecision Decision { get; set; } = ProviderApprovalDecision.Pending;
    public string? DecisionReason { get; set; }
    public string? RequestedChangesJson { get; set; }
    public DateTimeOffset SubmittedAtUtc { get; set; }
    public DateTimeOffset? ReviewStartedAtUtc { get; set; }
    public DateTimeOffset? DecidedAtUtc { get; set; }
    public Guid? ReviewedByAdminUserId { get; set; }

    public FreelancerApplication Application { get; set; } = null!;
}
