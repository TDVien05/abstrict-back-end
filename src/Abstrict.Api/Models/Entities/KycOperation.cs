using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class KycOperation : Entity
{
    public Guid UserId { get; set; }
    public Guid ApplicationId { get; set; }
    public KycOperationType Type { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string RequestHash { get; set; }
    public KycOperationState State { get; set; } = KycOperationState.Queued;
    public int AttemptCount { get; set; }
    public Guid? InputRevisionId { get; set; }
    public Guid? AttemptId { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public string? ErrorCode { get; set; }
    public string? ResultJson { get; set; }
    public string? ProviderRequestId { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }

    public FreelancerApplication Application { get; set; } = null!;
}
