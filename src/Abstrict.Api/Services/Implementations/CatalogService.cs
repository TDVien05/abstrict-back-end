using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Options;
using Abstrict.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Abstrict.Api.Services.Implementations;

public sealed class CatalogService(AppDbContext dbContext, IOptions<KycOptions> options) : ICatalogService
{
    private readonly KycOptions _kyc = options.Value;

    public async Task<IReadOnlyList<CatalogServiceAreaResponse>> GetServiceAreasAsync(string? city, CancellationToken cancellationToken)
    {
        var query = dbContext.ServiceAreas.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(x => x.City == city);

        return await query
            .OrderBy(x => x.City).ThenBy(x => x.District).ThenBy(x => x.WardOrComplex)
            .Select(x => new CatalogServiceAreaResponse(x.Id, x.City, x.District, x.WardOrComplex, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogServiceCategoryResponse>> GetServiceCategoriesAsync(CancellationToken cancellationToken) =>
        await dbContext.ServiceCategories.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new CatalogServiceCategoryResponse(x.Id, x.Code, x.Name, x.Description, x.IsActive))
            .ToListAsync(cancellationToken);

    public Task<IReadOnlyList<CatalogBankResponse>> GetBanksAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<CatalogBankResponse> banks = _kyc.Banks
            .OrderBy(x => x.Code)
            .Select(x => new CatalogBankResponse(x.Code, x.Name))
            .ToList();
        return Task.FromResult(banks);
    }
}
