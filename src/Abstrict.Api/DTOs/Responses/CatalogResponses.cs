namespace Abstrict.Api.DTOs.Responses;

public sealed record CatalogServiceAreaResponse(
    Guid Id,
    string City,
    string District,
    string? WardOrComplex,
    bool IsActive);

public sealed record CatalogServiceCategoryResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive);

public sealed record CatalogBankResponse(
    string Code,
    string Name);
