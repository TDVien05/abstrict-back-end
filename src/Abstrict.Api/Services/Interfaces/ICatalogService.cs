using Abstrict.Api.DTOs.Responses;

namespace Abstrict.Api.Services.Interfaces;

public interface ICatalogService
{
    Task<IReadOnlyList<CatalogServiceAreaResponse>> GetServiceAreasAsync(string? city, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogServiceCategoryResponse>> GetServiceCategoriesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogBankResponse>> GetBanksAsync(CancellationToken cancellationToken);
}
