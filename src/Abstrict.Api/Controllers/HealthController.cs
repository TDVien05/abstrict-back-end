using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Abstrict.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Version1 + "/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get() => Ok(new HealthResponse("Healthy"));
}
