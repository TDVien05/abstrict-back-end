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
    /// Lấy tỉnh/thành và quận/huyện của Việt Nam.
    /// </summary>
    /// <remarks>
    /// Dùng cho dropdown Thành phố / Quận-Huyện của form địa chỉ. <c>name</c> của tỉnh/thành và quận/huyện là giá trị gửi lên
    /// <c>city</c> và <c>district</c> khi lưu địa chỉ. <c>isServed</c> là <c>true</c> khi nền tảng có khu vực dịch vụ ở quận/huyện đó
    /// (chỉ những nơi này mới đặt lịch được). Lọc theo tên tỉnh/thành bằng <c>city</c>, ví dụ <c>?city=Hồ Chí Minh</c>.
    /// </remarks>
    [HttpGet("administrative-divisions")]
    [ProducesResponseType<IReadOnlyList<AdministrativeProvinceResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdministrativeProvinceResponse>>> GetAdministrativeDivisions([FromQuery] string? city, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "public, max-age=3600";
        return Ok(await catalogService.GetAdministrativeDivisionsAsync(city, cancellationToken));
    }

    /// <summary>
    /// Lấy phường/xã của một quận/huyện.
    /// </summary>
    /// <remarks>
    /// <c>provinceCode</c> và <c>districtCode</c> lấy từ <c>GET /catalog/administrative-divisions</c> (mã quận/huyện chỉ duy nhất trong một tỉnh). <c>name</c> là giá trị gửi lên <c>ward</c> khi lưu địa chỉ.
    /// Trả mảng rỗng khi chưa tải được dữ liệu phường/xã; khi đó FE cho nhập tay.
    /// </remarks>
    [HttpGet("administrative-divisions/provinces/{provinceCode:int}/districts/{districtCode:int}/wards")]
    [ProducesResponseType<IReadOnlyList<AdministrativeWardResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AdministrativeWardResponse>>> GetWards(int provinceCode, int districtCode, CancellationToken cancellationToken)
    {
        var wards = await catalogService.GetWardsAsync(provinceCode, districtCode, cancellationToken);
        if (wards is not null)
        {
            Response.Headers.CacheControl = "public, max-age=3600";
            return Ok(wards);
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "DISTRICT_NOT_FOUND",
            Detail = "Không tìm thấy quận/huyện."
        };
        problem.Extensions["code"] = "DISTRICT_NOT_FOUND";
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return NotFound(problem);
    }

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

    /// <summary>
    /// Lấy checklist của một danh mục dịch vụ.
    /// </summary>
    /// <remarks>
    /// Trả về template checklist đang hiệu lực (version cao nhất) theo mã danh mục, dùng để khách chọn phạm vi việc khi đặt lịch.
    /// </remarks>
    [HttpGet("service-categories/{code}/checklist")]
    [ProducesResponseType<ChecklistTemplateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChecklistTemplateResponse>> GetChecklist(string code, CancellationToken cancellationToken)
    {
        var checklist = await catalogService.GetChecklistAsync(code, cancellationToken);
        if (checklist is not null)
            return Ok(checklist);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "CHECKLIST_NOT_FOUND",
            Detail = "Danh mục này chưa có checklist."
        };
        problem.Extensions["code"] = "CHECKLIST_NOT_FOUND";
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return NotFound(problem);
    }
}
