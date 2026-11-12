using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Implementations;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abstrict.Api.Controllers;

/// <summary>Mọi thao tác quản trị: duyệt KYC freelancer, duyệt hồ sơ công ty, xử lý khiếu nại và cấu hình gói tăng hiển thị.</summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route(ApiRoutes.Version1 + "/admin")]
public sealed class AdminController(
    IAdminKycService kycService,
    IAdminCompanyReviewService companyReviewService,
    IDisputeService disputes,
    IPromotionPlanService plans) : ControllerBase
{
    #region KYC freelancer

    /// <param name="status">Bỏ trống = hàng chờ (Submitted + UnderReview).</param>
    /// <param name="page">Trang, bắt đầu từ 1.</param>
    /// <param name="pageSize">Số dòng mỗi trang.</param>
    /// <param name="cancellationToken">Token hủy.</param>
    [HttpGet("kyc")]
    public async Task<ActionResult<PagedResponse<AdminKycListItemResponse>>> ListKyc(
        ApprovalStatus? status, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await kycService.ListAsync(status, page, pageSize, cancellationToken));

    [HttpGet("kyc/{providerId:guid}")]
    public async Task<ActionResult<AdminKycDetailResponse>> GetKyc(Guid providerId, CancellationToken cancellationToken) =>
        Ok(await kycService.GetAsync(User.GetUserId(), providerId, cancellationToken));

    [HttpPost("kyc/{providerId:guid}/start-review")]
    public async Task<ActionResult<AdminKycDetailResponse>> StartKycReview(Guid providerId, CancellationToken cancellationToken) =>
        Ok(await kycService.StartReviewAsync(User.GetUserId(), providerId, cancellationToken));

    [HttpPost("kyc/{providerId:guid}/decision")]
    public async Task<ActionResult<AdminKycDetailResponse>> DecideKyc(
        Guid providerId, KycDecisionRequest request, CancellationToken cancellationToken) =>
        Ok(await kycService.DecideAsync(User.GetUserId(), providerId, request, cancellationToken));

    [HttpGet("kyc/{providerId:guid}/documents/{documentId:guid}/content")]
    public async Task<IActionResult> GetKycDocumentContent(Guid providerId, Guid documentId, CancellationToken cancellationToken)
    {
        var content = await kycService.OpenDocumentAsync(User.GetUserId(), providerId, documentId, cancellationToken);
        return content is null ? NotFound() : this.PrivateFile(content.Stream, content.ContentType);
    }

    #endregion

    #region Hồ sơ công ty
    // Admin xét duyệt hồ sơ đăng ký của công ty đối tác.

    /// <summary>Danh sách hồ sơ công ty, cũ nhất trước.</summary>
    /// <param name="status">Bỏ trống = hàng chờ (Submitted + UnderReview).</param>
    /// <param name="page">Trang, bắt đầu từ 1.</param>
    /// <param name="pageSize">Số dòng mỗi trang, tối đa 50.</param>
    /// <param name="cancellationToken">Token hủy.</param>
    [HttpGet("companies")]
    public async Task<ActionResult<PagedResponse<AdminCompanyListItemResponse>>> ListCompanies(
        ApprovalStatus? status, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await companyReviewService.ListAsync(status, page, pageSize, cancellationToken));

    /// <summary>Chi tiết hồ sơ, kèm số CCCD người đại diện đã giải mã. Có ghi audit.</summary>
    [HttpGet("companies/{providerId:guid}")]
    public async Task<ActionResult<AdminCompanyDetailResponse>> GetCompany(Guid providerId, CancellationToken cancellationToken) =>
        Ok(await companyReviewService.GetAsync(User.GetUserId(), providerId, cancellationToken));

    /// <summary>Bắt đầu xem xét: Submitted → UnderReview.</summary>
    [HttpPost("companies/{providerId:guid}/start-review")]
    public async Task<ActionResult<AdminCompanyDetailResponse>> StartCompanyReview(Guid providerId, CancellationToken cancellationToken) =>
        Ok(await companyReviewService.StartReviewAsync(User.GetUserId(), providerId, cancellationToken));

    /// <summary>Duyệt, từ chối hoặc yêu cầu chỉnh sửa. Lý do tối thiểu 10 ký tự trừ khi duyệt.</summary>
    [HttpPost("companies/{providerId:guid}/decision")]
    public async Task<ActionResult<AdminCompanyDetailResponse>> DecideCompany(
        Guid providerId, KycDecisionRequest request, CancellationToken cancellationToken) =>
        Ok(await companyReviewService.DecideAsync(User.GetUserId(), providerId, request, cancellationToken));

    /// <summary>Nội dung một giấy tờ của công ty. Có ghi audit.</summary>
    [HttpGet("companies/{providerId:guid}/documents/{documentId:guid}/content")]
    public async Task<IActionResult> GetCompanyDocumentContent(Guid providerId, Guid documentId, CancellationToken cancellationToken)
    {
        var content = await companyReviewService.OpenDocumentAsync(User.GetUserId(), providerId, documentId, cancellationToken)
            ?? throw new KycFlowException(StatusCodes.Status404NotFound, "DOCUMENT_NOT_FOUND", "Không tìm thấy giấy tờ.");
        return this.PrivateFile(content.Stream, content.ContentType);
    }

    #endregion

    #region Khiếu nại

    [HttpGet("disputes")]
    public async Task<ActionResult<PagedResponse<DisputeResponse>>> ListDisputes([FromQuery] DisputeStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await disputes.ListAdminAsync(status, page, pageSize, cancellationToken));

    [HttpGet("disputes/{disputeId:guid}")]
    public async Task<ActionResult<DisputeResponse>> GetDispute(Guid disputeId, CancellationToken cancellationToken) =>
        Ok(await disputes.GetAdminAsync(disputeId, cancellationToken));

    [HttpGet("disputes/{disputeId:guid}/evidence/{evidenceId:guid}/content")]
    public async Task<IActionResult> GetDisputeEvidenceContent(Guid disputeId, Guid evidenceId, CancellationToken cancellationToken)
    {
        var stream = await disputes.OpenEvidenceAsync(User.GetUserId(), true, disputeId, evidenceId, cancellationToken);
        if (stream is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(stream.Stream, stream.ContentType);
    }

    [HttpPost("disputes/{disputeId:guid}/messages")]
    public async Task<ActionResult<DisputeResponse>> AddDisputeMessage(Guid disputeId, AddDisputeMessageRequest request, [FromQuery] bool internalNote = false, CancellationToken cancellationToken = default) =>
        Ok(await disputes.AddAdminMessageAsync(User.GetUserId(), disputeId, request, internalNote, cancellationToken));

    [HttpPost("disputes/{disputeId:guid}/decision")]
    public async Task<ActionResult<DisputeResponse>> DecideDispute(Guid disputeId, DisputeDecisionRequest request, CancellationToken cancellationToken) =>
        Ok(await disputes.DecideAsync(User.GetUserId(), disputeId, request, cancellationToken));

    #endregion

    #region Gói tăng hiển thị
    // Admin cấu hình gói: giá, thời hạn, quyền lợi và đối tượng áp dụng (freelancer hoặc doanh nghiệp).

    /// <summary>Mọi gói, kể cả đang tắt. Lọc theo <c>audience</c> (<c>Freelancer</c>, <c>Company</c>) và <c>isActive</c>.</summary>
    [HttpGet("promotion-plans")]
    [ProducesResponseType<IReadOnlyList<AdminPromotionPlanResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminPromotionPlanResponse>>> ListPromotionPlans(
        [FromQuery] PromotionAudience? audience, [FromQuery] bool? isActive, CancellationToken cancellationToken) =>
        Ok(await plans.ListAdminAsync(audience, isActive, cancellationToken));

    /// <summary>Báo cáo gói: số gói, lượt mua theo trạng thái, doanh thu tổng và 7 ngày gần nhất, theo đối tượng và theo từng gói.</summary>
    [HttpGet("promotion-plans/report")]
    [ProducesResponseType<PromotionReportResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PromotionReportResponse>> GetPromotionReport(CancellationToken cancellationToken) =>
        Ok(await plans.GetReportAsync(cancellationToken));

    [HttpGet("promotion-plans/{planId:guid}")]
    [ProducesResponseType<AdminPromotionPlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminPromotionPlanResponse>> GetPromotionPlan(Guid planId, CancellationToken cancellationToken) =>
        Ok(await plans.GetAdminAsync(planId, cancellationToken));

    /// <summary>Tạo gói mới. <c>code</c> là duy nhất và không đổi được sau khi tạo; <c>audience</c> cũng không đổi được.</summary>
    [HttpPost("promotion-plans")]
    [ProducesResponseType<AdminPromotionPlanResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminPromotionPlanResponse>> CreatePromotionPlan(CreatePromotionPlanRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await plans.CreateAsync(User.GetUserId(), request, cancellationToken));

    /// <summary>Sửa tên, giá, thời hạn, quyền lợi và trạng thái bán. Lượt mua cũ giữ giá đã chốt lúc mua.</summary>
    [HttpPut("promotion-plans/{planId:guid}")]
    [ProducesResponseType<AdminPromotionPlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminPromotionPlanResponse>> UpdatePromotionPlan(Guid planId, UpdatePromotionPlanRequest request, CancellationToken cancellationToken) =>
        Ok(await plans.UpdateAsync(User.GetUserId(), planId, request, cancellationToken));

    /// <summary>Xóa gói chưa có lượt mua (204). Gói đã có lượt mua trả <c>PLAN_IN_USE</c>: hãy tắt gói.</summary>
    [HttpDelete("promotion-plans/{planId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeletePromotionPlan(Guid planId, CancellationToken cancellationToken)
    {
        await plans.DeleteAsync(User.GetUserId(), planId, cancellationToken);
        return NoContent();
    }

    #endregion
}
