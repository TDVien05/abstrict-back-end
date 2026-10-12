using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Abstrict.Api.Controllers;

/// <summary>Cổng thanh toán giả lập cho môi trường thử; trả 404 khi <c>Payments:SandboxEnabled</c> tắt.</summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Customer))]
[Route(ApiRoutes.Version1 + "/payments/sandbox")]
public sealed class SandboxPaymentsController(IBookingPaymentService paymentService) : BookingControllerBase
{
    /// <summary>Chốt kết quả thành công/thất bại cho một giao dịch sandbox của chính mình.</summary>
    [HttpPost("{paymentId:guid}/confirm")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<DepositPaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepositPaymentResponse>> Confirm(Guid paymentId, SandboxPaymentResultRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await paymentService.ConfirmSandboxAsync(CurrentUserId, paymentId, request.Succeeded, cancellationToken));
}
