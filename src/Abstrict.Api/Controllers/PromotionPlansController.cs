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

/// <summary>Admin cấu hình gói tăng hiển thị: giá, thời hạn, quyền lợi và đối tượng áp dụng (freelancer hoặc doanh nghiệp).</summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route(ApiRoutes.Version1 + "/admin/promotion-plans")]
public sealed class AdminPromotionPlansController(IPromotionPlanService plans) : BookingControllerBase
{
    /// <summary>Mọi gói, kể cả đang tắt. Lọc theo <c>audience</c> (<c>Freelancer</c>, <c>Company</c>) và <c>isActive</c>.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AdminPromotionPlanResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminPromotionPlanResponse>>> List(
        [FromQuery] PromotionAudience? audience, [FromQuery] bool? isActive, CancellationToken cancellationToken) =>
        await Run(async () => await plans.ListAdminAsync(audience, isActive, cancellationToken));

    /// <summary>Báo cáo gói: số gói, lượt mua theo trạng thái, doanh thu tổng và 7 ngày gần nhất, theo đối tượng và theo từng gói.</summary>
    [HttpGet("report")]
    [ProducesResponseType<PromotionReportResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PromotionReportResponse>> Report(CancellationToken cancellationToken) =>
        await Run(async () => await plans.GetReportAsync(cancellationToken));

    [HttpGet("{planId:guid}")]
    [ProducesResponseType<AdminPromotionPlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminPromotionPlanResponse>> Get(Guid planId, CancellationToken cancellationToken) =>
        await Run(async () => await plans.GetAdminAsync(planId, cancellationToken));

    /// <summary>Tạo gói mới. <c>code</c> là duy nhất và không đổi được sau khi tạo; <c>audience</c> cũng không đổi được.</summary>
    [HttpPost]
    [ProducesResponseType<AdminPromotionPlanResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminPromotionPlanResponse>> Create(CreatePromotionPlanRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await plans.CreateAsync(CurrentUserId, request, cancellationToken), StatusCodes.Status201Created);

    /// <summary>Sửa tên, giá, thời hạn, quyền lợi và trạng thái bán. Lượt mua cũ giữ giá đã chốt lúc mua.</summary>
    [HttpPut("{planId:guid}")]
    [ProducesResponseType<AdminPromotionPlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminPromotionPlanResponse>> Update(Guid planId, UpdatePromotionPlanRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await plans.UpdateAsync(CurrentUserId, planId, request, cancellationToken));

    /// <summary>Xóa gói chưa có lượt mua (204). Gói đã có lượt mua trả <c>PLAN_IN_USE</c>: hãy tắt gói.</summary>
    [HttpDelete("{planId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid planId, CancellationToken cancellationToken) =>
        await RunNoContent(async () => await plans.DeleteAsync(CurrentUserId, planId, cancellationToken));
}

/// <summary>Freelancer và doanh nghiệp xem các gói đang bán cho đúng đối tượng của mình.</summary>
[ApiController]
[Authorize(Roles = $"{nameof(UserRole.Freelancer)},{nameof(UserRole.Company)}")]
[Route(ApiRoutes.Version1 + "/partner/promotion-plans")]
public sealed class PartnerPromotionPlansController(IPromotionPlanService plans) : BookingControllerBase
{
    /// <summary>Gói đang bán, rẻ và ngắn hạn lên trước. Freelancer chỉ thấy gói <c>Freelancer</c>, doanh nghiệp chỉ thấy gói <c>Company</c>.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PartnerPromotionPlanResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PartnerPromotionPlanResponse>>> List(CancellationToken cancellationToken) =>
        await Run(async () => await plans.ListForPartnerAsync(CurrentUserId, cancellationToken));
}

/// <summary>Freelancer và doanh nghiệp mua gói tăng hiển thị và xem lịch sử mua.</summary>
[ApiController]
[Authorize(Roles = $"{nameof(UserRole.Freelancer)},{nameof(UserRole.Company)}")]
[Route(ApiRoutes.Version1 + "/partner/promotion-purchases")]
public sealed class PartnerPromotionPurchasesController(IPromotionPurchaseService purchases) : BookingControllerBase
{
    /// <summary>Lịch sử mua của đối tác, mới nhất trước. Lượt quá hạn có trạng thái <c>Expired</c>.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PromotionPurchaseResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PromotionPurchaseResponse>>> List(CancellationToken cancellationToken) =>
        await Run(async () => await purchases.ListAsync(CurrentUserId, cancellationToken));

    /// <summary>Mua gói. Gói miễn phí kích hoạt ngay; gói có phí trả lượt <c>PendingPayment</c> kèm <c>paymentId</c>.</summary>
    /// <remarks>Bắt buộc header <c>Idempotency-Key</c>; gọi lại cùng khóa trả về cùng lượt mua.</remarks>
    [HttpPost]
    [EnableRateLimiting("promotion-write")]
    [ProducesResponseType<PromotionPurchaseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PromotionPurchaseResponse>> Purchase(
        PurchasePromotionRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken) =>
        await Run(async () => await purchases.PurchaseAsync(
            CurrentUserId, request.PlanId ?? throw new BookingFlowException(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Thiếu gói cần mua."),
            request.Method, idempotencyKey, cancellationToken));

    /// <summary>Chốt kết quả thanh toán giả lập; 404 khi <c>Payments:SandboxEnabled</c> tắt.</summary>
    [HttpPost("{purchaseId:guid}/sandbox-confirm")]
    [EnableRateLimiting("promotion-write")]
    [ProducesResponseType<PromotionPurchaseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromotionPurchaseResponse>> ConfirmSandbox(Guid purchaseId, SandboxPaymentResultRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await purchases.ConfirmSandboxAsync(CurrentUserId, purchaseId, request.Succeeded, cancellationToken));
}
