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
[Authorize(Roles = nameof(UserRole.Freelancer))]
[Route(ApiRoutes.Version1 + "/freelancer/kyc")]
public sealed class FreelancerKycController(IFreelancerKycService kycService) : KycControllerBase
{
    [HttpGet]
    public async Task<ActionResult<FreelancerKycResponse>> Get(CancellationToken cancellationToken) =>
        await Run(async () => await kycService.GetAsync(CurrentUserId, cancellationToken));

    [HttpPut("profile")]
    [EnableRateLimiting("kyc-write")]
    public async Task<ActionResult<FreelancerKycResponse>> SaveProfile(SaveFreelancerKycProfileRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await kycService.SaveProfileAsync(CurrentUserId, request, cancellationToken));

    [HttpPut("documents/{type}")]
    [EnableRateLimiting("kyc-write")]
    [RequestSizeLimit(KycImage.MaximumBytes + 256 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<KycDocumentResponse>> UploadDocument(
        VerificationDocumentType type, IFormFile file, CancellationToken cancellationToken) =>
        await Run(async () =>
        {
            await using var stream = file.OpenReadStream();
            return await kycService.UploadDocumentAsync(CurrentUserId, type, file.FileName, file.Length, stream, cancellationToken);
        });

    [HttpPost("submit")]
    [EnableRateLimiting("kyc-write")]
    public async Task<ActionResult<FreelancerKycResponse>> Submit(CancellationToken cancellationToken) =>
        await Run(async () => await kycService.SubmitAsync(CurrentUserId, cancellationToken));

    [HttpGet("documents/{documentId:guid}/content")]
    public async Task<IActionResult> GetDocumentContent(Guid documentId, CancellationToken cancellationToken)
    {
        try
        {
            var content = await kycService.OpenDocumentAsync(CurrentUserId, documentId, cancellationToken);
            return content is null ? NotFound() : ImageFile(content);
        }
        catch (KycFlowException exception)
        {
            return ToProblem(exception);
        }
    }
}
