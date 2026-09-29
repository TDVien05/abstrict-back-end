using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Abstrict.Api.Controllers;

/// <summary>
/// Dữ liệu danh mục dùng chung (khu vực dịch vụ, danh mục dịch vụ, ngân hàng).
/// </summary>
/// <remarks>
/// Các endpoint công khai cung cấp dữ liệu tham chiếu cho form đăng ký và onboarding.
/// </remarks>
[ApiController]
[Route(ApiRoutes.Version1 + "/catalog")]
public sealed class CatalogController(ICatalogService catalogService) : ControllerBase
{
    /// <summary>
    /// Lấy danh sách khu vực dịch vụ.
    /// </summary>
    /// <remarks>
    /// Trả về các khu vực dịch vụ, có thể lọc theo thành phố qua tham số <c>city</c>.
    /// </remarks>
    [HttpGet("service-areas")]
    [ProducesResponseType<IReadOnlyList<CatalogServiceAreaResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CatalogServiceAreaResponse>>> GetServiceAreas([FromQuery] string? city, CancellationToken cancellationToken) =>
        Ok(await catalogService.GetServiceAreasAsync(city, cancellationToken));

    /// <summary>
    /// Lấy danh sách danh mục dịch vụ.
    /// </summary>
    /// <remarks>
    /// Trả về tất cả danh mục dịch vụ mà freelancer có thể đăng ký cung cấp.
    /// </remarks>
    [HttpGet("service-categories")]
    [ProducesResponseType<IReadOnlyList<CatalogServiceCategoryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CatalogServiceCategoryResponse>>> GetServiceCategories(CancellationToken cancellationToken) =>
        Ok(await catalogService.GetServiceCategoriesAsync(cancellationToken));

    /// <summary>
    /// Lấy danh sách ngân hàng.
    /// </summary>
    /// <remarks>
    /// Trả về danh sách ngân hàng hỗ trợ để freelancer khai báo thông tin tài khoản nhận tiền.
    /// </remarks>
    [HttpGet("banks")]
    [ProducesResponseType<IReadOnlyList<CatalogBankResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CatalogBankResponse>>> GetBanks(CancellationToken cancellationToken) =>
        Ok(await catalogService.GetBanksAsync(cancellationToken));
}
