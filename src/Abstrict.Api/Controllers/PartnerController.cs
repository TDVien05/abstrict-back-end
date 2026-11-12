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

/// <summary>Thao tác chung của đối tác (freelancer và doanh nghiệp): xem gói tăng hiển thị, mua gói và lịch sử mua.</summary>
[ApiController]
[Authorize(Roles = $"{nameof(UserRole.Freelancer)},{nameof(UserRole.Company)}")]
[Route(ApiRoutes.Version1 + "/partner")]
public sealed class PartnerController(IPromotionPlanService plans, IPromotionPurchaseService purchases) : ControllerBase
{
    /// <summary>Gói đang bán, rẻ và ngắn hạn lên trước. Freelancer chỉ thấy gói <c>Freelancer</c>, doanh nghiệp chỉ thấy gói <c>Company</c>.</summary>
    [HttpGet("promotion-plans")]
    [ProducesResponseType<IReadOnlyList<PartnerPromotionPlanResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PartnerPromotionPlanResponse>>> ListPromotionPlans(CancellationToken cancellationToken) =>
        Ok(await plans.ListForPartnerAsync(User.GetUserId(), cancellationToken));

    /// <summary>Lịch sử mua của đối tác, mới nhất trước. Lượt quá hạn có trạng thái <c>Expired</c>.</summary>
    [HttpGet("promotion-purchases")]
    [ProducesResponseType<IReadOnlyList<PromotionPurchaseResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PromotionPurchaseResponse>>> ListPromotionPurchases(CancellationToken cancellationToken) =>
        Ok(await purchases.ListAsync(User.GetUserId(), cancellationToken));

    /// <summary>Mua gói. Gói miễn phí kích hoạt ngay; gói có phí trả lượt <c>PendingPayment</c> kèm <c>paymentId</c>.</summary>
    /// <remarks>Bắt buộc header <c>Idempotency-Key</c>; gọi lại cùng khóa trả về cùng lượt mua.</remarks>
    [HttpPost("promotion-purchases")]
    [EnableRateLimiting("promotion-write")]
    [ProducesResponseType<PromotionPurchaseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PromotionPurchaseResponse>> PurchasePromotion(
        PurchasePromotionRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken) =>
        Ok(await purchases.PurchaseAsync(
            User.GetUserId(), request.PlanId ?? throw new BookingFlowException(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Thiếu gói cần mua."),
            request.Method, idempotencyKey, cancellationToken));

    /// <summary>Chốt kết quả thanh toán giả lập; 404 khi <c>Payments:SandboxEnabled</c> tắt.</summary>
    [HttpPost("promotion-purchases/{purchaseId:guid}/sandbox-confirm")]
    [EnableRateLimiting("promotion-write")]
    [ProducesResponseType<PromotionPurchaseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromotionPurchaseResponse>> ConfirmPromotionSandbox(Guid purchaseId, SandboxPaymentResultRequest request, CancellationToken cancellationToken) =>
        Ok(await purchases.ConfirmSandboxAsync(User.GetUserId(), purchaseId, request.Succeeded, cancellationToken));
}
