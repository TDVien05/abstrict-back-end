namespace Abstrict.Api.DTOs.Responses;

public sealed record PagedResponse<T>(
    IReadOnlyCollection<T> Items,
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages);
