using System.ComponentModel.DataAnnotations;
using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Implementations;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Abstrict.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Customer))]
[Route(ApiRoutes.Version1 + "/disputes")]
public sealed class DisputesController(IDisputeService disputes) : BookingControllerBase
{
    [HttpGet("categories")]
    public ActionResult<IReadOnlyCollection<DisputeCategoryResponse>> Categories() => Ok(disputes.GetCategories());

    [HttpGet]
    public Task<ActionResult<PagedResponse<DisputeResponse>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Run(async () => await disputes.ListCustomerAsync(CurrentUserId, page, pageSize, cancellationToken));

    [HttpGet("{disputeId:guid}")]
    public Task<ActionResult<DisputeResponse>> Get(Guid disputeId, CancellationToken cancellationToken) =>
        Run(async () => await disputes.GetCustomerAsync(CurrentUserId, disputeId, cancellationToken));

    [HttpPost("bookings/{bookingId:guid}")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<DisputeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<DisputeResponse>> Open(Guid bookingId, OpenDisputeRequest request, CancellationToken cancellationToken) =>
        Run(async () => await disputes.OpenAsync(CurrentUserId, bookingId, request, cancellationToken));

    [HttpPost("{disputeId:guid}/evidence")]
    [EnableRateLimiting("booking-write")]
    [RequestSizeLimit(KycImage.MaximumBytes + 256 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DisputeEvidenceResponse>> UploadEvidence(Guid disputeId, IFormFile file, [FromForm, StringLength(300)] string? description, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            return Ok(await disputes.UploadEvidenceAsync(CurrentUserId, disputeId, description, file.Length, stream, cancellationToken));
        }
        catch (BookingFlowException exception) { return ToProblem(exception); }
    }

    [HttpGet("{disputeId:guid}/evidence/{evidenceId:guid}/content")]
    public async Task<IActionResult> EvidenceContent(Guid disputeId, Guid evidenceId, CancellationToken cancellationToken)
    {
        var stream = await disputes.OpenEvidenceAsync(CurrentUserId, false, disputeId, evidenceId, cancellationToken);
        if (stream is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(stream.Stream, stream.ContentType);
    }

    [HttpPost("{disputeId:guid}/messages")]
    [EnableRateLimiting("booking-write")]
    public Task<ActionResult<DisputeResponse>> Message(Guid disputeId, AddDisputeMessageRequest request, CancellationToken cancellationToken) =>
        Run(async () => await disputes.AddCustomerMessageAsync(CurrentUserId, disputeId, request, cancellationToken));
}

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route(ApiRoutes.Version1 + "/admin/disputes")]
public sealed class AdminDisputesController(IDisputeService disputes) : BookingControllerBase
{
    [HttpGet]
    public Task<ActionResult<PagedResponse<DisputeResponse>>> List([FromQuery] DisputeStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Run(async () => await disputes.ListAdminAsync(status, page, pageSize, cancellationToken));

    [HttpGet("{disputeId:guid}")]
    public Task<ActionResult<DisputeResponse>> Get(Guid disputeId, CancellationToken cancellationToken) =>
        Run(async () => await disputes.GetAdminAsync(disputeId, cancellationToken));

    [HttpGet("{disputeId:guid}/evidence/{evidenceId:guid}/content")]
    public async Task<IActionResult> EvidenceContent(Guid disputeId, Guid evidenceId, CancellationToken cancellationToken)
    {
        var stream = await disputes.OpenEvidenceAsync(CurrentUserId, true, disputeId, evidenceId, cancellationToken);
        if (stream is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(stream.Stream, stream.ContentType);
    }

    [HttpPost("{disputeId:guid}/messages")]
    public Task<ActionResult<DisputeResponse>> Message(Guid disputeId, AddDisputeMessageRequest request, [FromQuery] bool internalNote = false, CancellationToken cancellationToken = default) =>
        Run(async () => await disputes.AddAdminMessageAsync(CurrentUserId, disputeId, request, internalNote, cancellationToken));

    [HttpPost("{disputeId:guid}/decision")]
    public Task<ActionResult<DisputeResponse>> Decide(Guid disputeId, DisputeDecisionRequest request, CancellationToken cancellationToken) =>
        Run(async () => await disputes.DecideAsync(CurrentUserId, disputeId, request, cancellationToken));
}
