using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Abstrict.Api.Controllers;

/// <summary>
/// Kiểm tra tình trạng hoạt động của API.
/// </summary>
[ApiController]
[Route(ApiRoutes.Version1 + "/health")]
public sealed class HealthController : ControllerBase
{
    /// <summary>
    /// Lấy trạng thái sức khỏe của API.
    /// </summary>
    /// <remarks>
    /// Trả về trạng thái "Healthy" khi API hoạt động bình thường.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get() => Ok(new HealthResponse("Healthy"));
}
