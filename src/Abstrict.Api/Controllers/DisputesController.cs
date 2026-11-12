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
public sealed class DisputesController(IDisputeService disputes) : ControllerBase
{
    [HttpGet("categories")]
    public ActionResult<IReadOnlyCollection<DisputeCategoryResponse>> Categories() => Ok(disputes.GetCategories());

    [HttpGet]
    public async Task<ActionResult<PagedResponse<DisputeResponse>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await disputes.ListCustomerAsync(User.GetUserId(), page, pageSize, cancellationToken));

    [HttpGet("{disputeId:guid}")]
    public async Task<ActionResult<DisputeResponse>> Get(Guid disputeId, CancellationToken cancellationToken) =>
        Ok(await disputes.GetCustomerAsync(User.GetUserId(), disputeId, cancellationToken));

    [HttpPost("bookings/{bookingId:guid}")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<DisputeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DisputeResponse>> Open(Guid bookingId, OpenDisputeRequest request, CancellationToken cancellationToken) =>
        Ok(await disputes.OpenAsync(User.GetUserId(), bookingId, request, cancellationToken));

    [HttpPost("{disputeId:guid}/evidence")]
    [EnableRateLimiting("booking-write")]
    [RequestSizeLimit(KycImage.MaximumBytes + 256 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DisputeEvidenceResponse>> UploadEvidence(Guid disputeId, IFormFile file, [FromForm, StringLength(300)] string? description, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await disputes.UploadEvidenceAsync(User.GetUserId(), disputeId, description, file.Length, stream, cancellationToken));
    }

    [HttpGet("{disputeId:guid}/evidence/{evidenceId:guid}/content")]
    public async Task<IActionResult> EvidenceContent(Guid disputeId, Guid evidenceId, CancellationToken cancellationToken)
    {
        var stream = await disputes.OpenEvidenceAsync(User.GetUserId(), false, disputeId, evidenceId, cancellationToken);
        if (stream is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(stream.Stream, stream.ContentType);
    }

    [HttpPost("{disputeId:guid}/messages")]
    [EnableRateLimiting("booking-write")]
    public async Task<ActionResult<DisputeResponse>> Message(Guid disputeId, AddDisputeMessageRequest request, CancellationToken cancellationToken) =>
        Ok(await disputes.AddCustomerMessageAsync(User.GetUserId(), disputeId, request, cancellationToken));
}
