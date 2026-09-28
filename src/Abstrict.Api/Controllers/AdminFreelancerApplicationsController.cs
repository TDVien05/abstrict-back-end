using System.Security.Claims;
using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abstrict.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route(ApiRoutes.Version1 + "/admin/freelancer-applications")]
public sealed class AdminFreelancerApplicationsController(IProviderApprovalService approvalService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<AdminFreelancerApplicationSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AdminFreelancerApplicationSummaryResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await approvalService.ListAsync(status, page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AdminFreelancerApplicationDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminFreelancerApplicationDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var detail = await approvalService.GetDetailAsync(id, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpGet("{id:guid}/documents/{documentId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocumentContent(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        var result = await approvalService.GetDocumentContentAsync(id, documentId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (result is null)
            return NotFound();
        return File(result.Stream, result.ContentType, result.FileName);
    }

    [HttpPost("{id:guid}/start-review")]
    [ProducesResponseType<AdminFreelancerApplicationDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminFreelancerApplicationDetailResponse>> StartReview(Guid id, StartReviewRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await approvalService.StartReviewAsync(id, request.SubmissionVersion, CurrentAdminId(), cancellationToken);
            return detail is null ? NotFound() : Ok(detail);
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    [HttpPost("{id:guid}/decision")]
    [ProducesResponseType<AdminFreelancerApplicationDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminFreelancerApplicationDetailResponse>> Decide(Guid id, AdminApplicationDecisionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await approvalService.DecideAsync(id, request, CurrentAdminId(), cancellationToken);
            return detail is null ? NotFound() : Ok(detail);
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    private Guid CurrentAdminId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new ApiFlowException(StatusCodes.Status401Unauthorized, "UNAUTHENTICATED", "Thiếu thông tin xác thực."));
}
