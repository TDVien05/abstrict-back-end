using System.Security.Claims;
using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace Abstrict.Api.Controllers;

[ApiController]
[Authorize(Roles = "Freelancer")]
[Route(ApiRoutes.Version1 + "/freelancers/me/onboarding")]
public sealed class FreelancerOnboardingController(IFreelancerOnboardingService onboardingService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<FreelancerOnboardingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FreelancerOnboardingResponse>> Get(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await onboardingService.GetOnboardingAsync(CurrentUserId(), cancellationToken));
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    [HttpPatch]
    [ProducesResponseType<FreelancerOnboardingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FreelancerOnboardingResponse>> Patch(UpdateFreelancerOnboardingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await onboardingService.UpdateOnboardingAsync(CurrentUserId(), request, ExpectedVersion(), cancellationToken));
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    [HttpPost("consents")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RecordConsent(RecordKycConsentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await onboardingService.RecordConsentAsync(CurrentUserId(), request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
            return NoContent();
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    [HttpPost("documents")]
    [RequestSizeLimit(20_000_000)]
    [ProducesResponseType<FreelancerDocumentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<FreelancerDocumentResponse>> UploadDocument([FromForm] UploadOnboardingDocumentRequest metadata, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return this.ToFlowProblem(new ApiFlowException(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Thiếu tệp tải lên."));

        try
        {
            await using var stream = file.OpenReadStream();
            var response = await onboardingService.UploadDocumentAsync(
                CurrentUserId(), stream, file.FileName, file.ContentType, metadata, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = response.DocumentId }, response);
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    [HttpGet("documents/{id:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocumentContent(Guid id, CancellationToken cancellationToken)
    {
        var result = await onboardingService.GetDocumentContentAsync(CurrentUserId(), id, cancellationToken);
        if (result is null)
            return NotFound();
        return File(result.Stream, result.ContentType, result.FileName);
    }

    [HttpDelete("documents/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteDocument(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await onboardingService.DeleteDocumentAsync(CurrentUserId(), id, ExpectedVersion(), cancellationToken);
            return NoContent();
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    [HttpPost("identity/ocr")]
    [EnableRateLimiting("kyc-operation")]
    [ProducesResponseType<KycOperationResponse>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<KycOperationResponse>> StartOcr(StartIdentityOcrRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await onboardingService.StartOcrAsync(CurrentUserId(), request, IdempotencyKey(), ExpectedVersion(), cancellationToken);
            return Accepted(response);
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    [HttpPost("identity/confirm")]
    [ProducesResponseType<FreelancerOnboardingResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FreelancerOnboardingResponse>> ConfirmIdentity(ConfirmIdentityRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await onboardingService.ConfirmIdentityAsync(CurrentUserId(), request, cancellationToken));
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    [HttpPost("identity/face-match")]
    [EnableRateLimiting("kyc-operation")]
    [ProducesResponseType<KycOperationResponse>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<KycOperationResponse>> StartFaceMatch(StartFaceMatchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await onboardingService.StartFaceMatchAsync(CurrentUserId(), request, IdempotencyKey(), cancellationToken);
            return Accepted(response);
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    [HttpGet("operations/{id:guid}")]
    [ProducesResponseType<KycOperationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KycOperationResponse>> GetOperation(Guid id, CancellationToken cancellationToken)
    {
        var response = await onboardingService.GetOperationAsync(CurrentUserId(), id, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("submit")]
    [ProducesResponseType<FreelancerSubmissionSummaryResponse>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<FreelancerSubmissionSummaryResponse>> Submit(SubmitApplicationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await onboardingService.SubmitAsync(CurrentUserId(), request, ExpectedVersion(), cancellationToken);
            return Accepted(response);
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    [HttpPost("reopen")]
    [ProducesResponseType<FreelancerOnboardingResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FreelancerOnboardingResponse>> Reopen(ReopenApplicationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await onboardingService.ReopenAsync(CurrentUserId(), request, cancellationToken));
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    private Guid CurrentUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new ApiFlowException(StatusCodes.Status401Unauthorized, "UNAUTHENTICATED", "Thiếu thông tin xác thực."));

    private int? ExpectedVersion()
    {
        if (!Request.Headers.TryGetValue(HeaderNames.IfMatch, out var values))
            return null;

        var raw = values.ToString().Trim().Trim('"');
        return int.TryParse(raw, out var version) ? version : null;
    }

    private string? IdempotencyKey() =>
        Request.Headers.TryGetValue("Idempotency-Key", out var values) && !string.IsNullOrWhiteSpace(values)
            ? values.ToString()
            : null;
}
