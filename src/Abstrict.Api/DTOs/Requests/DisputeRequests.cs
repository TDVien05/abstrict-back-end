using System.ComponentModel.DataAnnotations;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.DTOs.Requests;

public sealed class OpenDisputeRequest
{
    public DisputeCategory Category { get; init; }

    [Required, StringLength(4000, MinimumLength = 10)]
    public required string Description { get; init; }
}

public sealed class AddDisputeMessageRequest
{
    [Required, StringLength(4000, MinimumLength = 1)]
    public required string Body { get; init; }
}

public sealed class DisputeDecisionRequest
{
    [Required, StringLength(80, MinimumLength = 2)]
    public required string OutcomeCode { get; init; }

    [Required, StringLength(4000, MinimumLength = 5)]
    public required string Reason { get; init; }

    [Range(0, long.MaxValue)]
    public long? RefundAmountVnd { get; init; }

    [Range(0, long.MaxValue)]
    public long? ProviderAmountVnd { get; init; }
}
