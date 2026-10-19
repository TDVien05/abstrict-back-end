namespace Abstrict.Api.DTOs.Responses;

public sealed record RatingResponse(
    Guid Id,
    Guid BookingId,
    string BookingNumber,
    Guid ProviderId,
    string ProviderName,
    int Score,
    string? Comment,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PublishedAtUtc);

public sealed record PublicRatingResponse(
    Guid Id,
    Guid BookingId,
    string BookingNumber,
    int Score,
    string? Comment,
    DateTimeOffset PublishedAtUtc);
