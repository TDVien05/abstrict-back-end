using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abstrict.Api.Controllers;

/// <summary>Danh bạ freelancer công khai (chỉ hồ sơ đã duyệt, không lộ dữ liệu cá nhân/KYC).</summary>
[ApiController]
[AllowAnonymous]
[Route(ApiRoutes.Version1 + "/freelancers")]
public sealed class FreelancersController(IFreelancerDirectoryService directoryService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<FreelancerSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<FreelancerSummaryResponse>>> Search(
        [FromQuery] string? serviceCode, [FromQuery] string? city, [FromQuery] string? district,
        [FromQuery] double? minRating, [FromQuery] bool? acceptingBookings,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await directoryService.SearchAsync(serviceCode, city, district, minRating, acceptingBookings, page, pageSize, cancellationToken));

    [HttpGet("{providerId:guid}")]
    [ProducesResponseType<FreelancerDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FreelancerDetailResponse>> Get(Guid providerId, CancellationToken cancellationToken)
    {
        var freelancer = await directoryService.GetAsync(providerId, cancellationToken);
        if (freelancer is not null)
            return Ok(freelancer);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "FREELANCER_NOT_FOUND",
            Detail = "Không tìm thấy freelancer."
        };
        problem.Extensions["code"] = "FREELANCER_NOT_FOUND";
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return NotFound(problem);
    }
}
