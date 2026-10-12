using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Implementations;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abstrict.Api.Controllers;

/// <summary>Admin xét duyệt hồ sơ đăng ký của công ty đối tác.</summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route(ApiRoutes.Version1 + "/admin/companies")]
public sealed class AdminCompanyController(IAdminCompanyReviewService reviewService) : KycControllerBase
{
    /// <summary>Danh sách hồ sơ công ty, cũ nhất trước.</summary>
    /// <param name="status">Bỏ trống = hàng chờ (Submitted + UnderReview).</param>
    /// <param name="page">Trang, bắt đầu từ 1.</param>
    /// <param name="pageSize">Số dòng mỗi trang, tối đa 50.</param>
    /// <param name="cancellationToken">Token hủy.</param>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AdminCompanyListItemResponse>>> List(
        ApprovalStatus? status, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
        await Run(async () => await reviewService.ListAsync(status, page, pageSize, cancellationToken));

    /// <summary>Chi tiết hồ sơ, kèm số CCCD người đại diện đã giải mã. Có ghi audit.</summary>
    [HttpGet("{providerId:guid}")]
    public async Task<ActionResult<AdminCompanyDetailResponse>> Get(Guid providerId, CancellationToken cancellationToken) =>
        await Run(async () => await reviewService.GetAsync(CurrentUserId, providerId, cancellationToken));

    /// <summary>Bắt đầu xem xét: Submitted → UnderReview.</summary>
    [HttpPost("{providerId:guid}/start-review")]
    public async Task<ActionResult<AdminCompanyDetailResponse>> StartReview(Guid providerId, CancellationToken cancellationToken) =>
        await Run(async () => await reviewService.StartReviewAsync(CurrentUserId, providerId, cancellationToken));

    /// <summary>Duyệt, từ chối hoặc yêu cầu chỉnh sửa. Lý do tối thiểu 10 ký tự trừ khi duyệt.</summary>
    [HttpPost("{providerId:guid}/decision")]
    public async Task<ActionResult<AdminCompanyDetailResponse>> Decide(
        Guid providerId, KycDecisionRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await reviewService.DecideAsync(CurrentUserId, providerId, request, cancellationToken));

    /// <summary>Nội dung một giấy tờ của công ty. Có ghi audit.</summary>
    [HttpGet("{providerId:guid}/documents/{documentId:guid}/content")]
    public async Task<IActionResult> GetDocumentContent(Guid providerId, Guid documentId, CancellationToken cancellationToken)
    {
        try
        {
            var content = await reviewService.OpenDocumentAsync(CurrentUserId, providerId, documentId, cancellationToken);
            return content is null
                ? ToProblem(new KycFlowException(StatusCodes.Status404NotFound, "DOCUMENT_NOT_FOUND", "Không tìm thấy giấy tờ."))
                : ImageFile(content);
        }
        catch (KycFlowException exception)
        {
            return ToProblem(exception);
        }
    }
}
