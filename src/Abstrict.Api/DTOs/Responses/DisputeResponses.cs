using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.DTOs.Responses;

public sealed record DisputeEvidenceResponse(Guid Id, Guid UploadedByUserId, string? Description, DateTimeOffset CreatedAtUtc, string ContentUrl);
public sealed record DisputeMessageResponse(Guid Id, Guid AuthorUserId, string Body, bool IsInternalAdminNote, DateTimeOffset CreatedAtUtc);
public sealed record DisputeDecisionResponse(Guid Id, Guid AdminUserId, DisputeDecisionType Type, string OutcomeCode, string Reason, long? RefundAmountVnd, long? ProviderAmountVnd, DateTimeOffset DecidedAtUtc);

public sealed record DisputeResponse(
    Guid Id, Guid BookingId, string BookingNumber, BookingStatus BookingStatus,
    Guid CustomerUserId, string CustomerName, Guid ProviderId, string ProviderName,
    DisputeCategory Category, DisputeStatus Status, string Description,
    DateTimeOffset ResponseDeadlineUtc, DateTimeOffset CreatedAtUtc,
    IReadOnlyCollection<DisputeEvidenceResponse> Evidence,
    IReadOnlyCollection<DisputeMessageResponse> Messages,
    IReadOnlyCollection<DisputeDecisionResponse> Decisions);

public sealed record DisputeCategoryResponse(DisputeCategory Value, string Code, string Label, string Description);
