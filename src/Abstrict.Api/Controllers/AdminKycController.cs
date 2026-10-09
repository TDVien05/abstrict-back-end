using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Implementations;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abstrict.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route(ApiRoutes.Version1 + "/admin/kyc")]
public sealed class AdminKycController(IAdminKycService kycService) : KycControllerBase
{
    /// <param name="status">Bỏ trống = hàng chờ (Submitted + UnderReview).</param>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AdminKycListItemResponse>>> List(
        ApprovalStatus? status, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
        await Run(async () => await kycService.ListAsync(status, page, pageSize, cancellationToken));

    [HttpGet("{providerId:guid}")]
    public async Task<ActionResult<AdminKycDetailResponse>> Get(Guid providerId, CancellationToken cancellationToken) =>
        await Run(async () => await kycService.GetAsync(CurrentUserId, providerId, cancellationToken));

    [HttpPost("{providerId:guid}/start-review")]
    public async Task<ActionResult<AdminKycDetailResponse>> StartReview(Guid providerId, CancellationToken cancellationToken) =>
        await Run(async () => await kycService.StartReviewAsync(CurrentUserId, providerId, cancellationToken));

    [HttpPost("{providerId:guid}/decision")]
    public async Task<ActionResult<AdminKycDetailResponse>> Decide(
        Guid providerId, KycDecisionRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await kycService.DecideAsync(CurrentUserId, providerId, request, cancellationToken));

    [HttpGet("{providerId:guid}/documents/{documentId:guid}/content")]
    public async Task<IActionResult> GetDocumentContent(Guid providerId, Guid documentId, CancellationToken cancellationToken)
    {
        try
        {
            var content = await kycService.OpenDocumentAsync(CurrentUserId, providerId, documentId, cancellationToken);
            return content is null ? NotFound() : ImageFile(content);
        }
        catch (KycFlowException exception)
        {
            return ToProblem(exception);
        }
    }
}
