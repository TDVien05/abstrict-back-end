using System.ComponentModel.DataAnnotations;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.DTOs.Requests;

public sealed class StartReviewRequest
{
    [Required]
    public int SubmissionVersion { get; init; }
}

public sealed class AdminApplicationDecisionRequest
{
    [Required]
    public int SubmissionVersion { get; init; }

    [Required]
    public ProviderApprovalDecision? Decision { get; init; }

    [StringLength(1000)]
    public string? Reason { get; init; }

    public IReadOnlyList<string>? RequestedChanges { get; init; }
}
