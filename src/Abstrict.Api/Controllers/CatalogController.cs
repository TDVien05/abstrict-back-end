using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Abstrict.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Version1 + "/catalog")]
public sealed class CatalogController(ICatalogService catalogService) : ControllerBase
{
    [HttpGet("service-areas")]
    [ProducesResponseType<IReadOnlyList<CatalogServiceAreaResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CatalogServiceAreaResponse>>> GetServiceAreas([FromQuery] string? city, CancellationToken cancellationToken) =>
        Ok(await catalogService.GetServiceAreasAsync(city, cancellationToken));

    [HttpGet("service-categories")]
    [ProducesResponseType<IReadOnlyList<CatalogServiceCategoryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CatalogServiceCategoryResponse>>> GetServiceCategories(CancellationToken cancellationToken) =>
        Ok(await catalogService.GetServiceCategoriesAsync(cancellationToken));

    [HttpGet("banks")]
    [ProducesResponseType<IReadOnlyList<CatalogBankResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CatalogBankResponse>>> GetBanks(CancellationToken cancellationToken) =>
        Ok(await catalogService.GetBanksAsync(cancellationToken));
}
