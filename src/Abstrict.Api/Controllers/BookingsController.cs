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

/// <summary>
/// Đặt lịch phía khách hàng.
/// </summary>
/// <remarks>
/// Báo giá, tạo đơn giữ chỗ, đặt cọc, xem, hủy và lấy mã QR check-in. Giá, lịch và trạng thái luôn do server quyết định.
/// </remarks>
[ApiController]
[Authorize(Roles = nameof(UserRole.Customer))]
[Route(ApiRoutes.Version1 + "/bookings")]
public sealed class BookingsController(IBookingService bookingService, IBookingPaymentService paymentService) : BookingControllerBase
{
    /// <summary>Báo giá một ca làm, không tạo đơn.</summary>
    /// <remarks>Kiểm tra đầy đủ đối tác, dịch vụ, địa chỉ và khung giờ như khi tạo đơn.</remarks>
    [HttpPost("quote")]
    [ProducesResponseType<BookingQuoteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingQuoteResponse>> Quote(CreateBookingRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await bookingService.QuoteAsync(CurrentUserId, request, cancellationToken));

    /// <summary>Tạo đơn ở trạng thái chờ đặt cọc và giữ chỗ trong thời gian thanh toán.</summary>
    [HttpPost]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<BookingDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDetailResponse>> Create(CreateBookingRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await bookingService.CreateAsync(CurrentUserId, request, cancellationToken), StatusCodes.Status201Created);

    /// <summary>Danh sách đơn của tôi, giờ bắt đầu mới nhất trước.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<BookingSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<BookingSummaryResponse>>> List(
        [FromQuery] BookingStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        await Run(async () => await bookingService.ListAsync(CurrentUserId, status, page, pageSize, cancellationToken));

    /// <summary>Chi tiết đơn kèm checklist, dòng thời gian và các thao tác được phép.</summary>
    [HttpGet("{bookingId:guid}")]
    [ProducesResponseType<BookingDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailResponse>> Get(Guid bookingId, CancellationToken cancellationToken) =>
        await Run(async () => await bookingService.GetAsync(CurrentUserId, bookingId, cancellationToken));

    /// <summary>Khởi tạo thanh toán cọc.</summary>
    /// <remarks>Bắt buộc header <c>Idempotency-Key</c>; gọi lại cùng khóa trả về cùng giao dịch.</remarks>
    [HttpPost("{bookingId:guid}/deposit")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<DepositPaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DepositPaymentResponse>> Deposit(
        Guid bookingId, InitiateDepositRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken) =>
        await Run(async () => await paymentService.InitiateDepositAsync(CurrentUserId, bookingId, idempotencyKey, request.Method, cancellationToken));

    /// <summary>Xem trước số tiền hoàn/giữ nếu hủy ngay bây giờ.</summary>
    [HttpGet("{bookingId:guid}/cancellation-preview")]
    [ProducesResponseType<CancellationPreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CancellationPreviewResponse>> PreviewCancellation(Guid bookingId, CancellationToken cancellationToken) =>
        await Run(async () => await bookingService.PreviewCancellationAsync(CurrentUserId, bookingId, cancellationToken));

    /// <summary>Hủy đơn.</summary>
    /// <remarks>Còn từ 2 giờ trở lên trước giờ bắt đầu thì hoàn đủ cọc; dưới 2 giờ thì không hoàn cọc.</remarks>
    [HttpPost("{bookingId:guid}/cancel")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<BookingDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDetailResponse>> Cancel(Guid bookingId, CancelBookingRequest? request, CancellationToken cancellationToken) =>
        await Run(async () => await bookingService.CancelAsync(CurrentUserId, bookingId, request?.Reason, cancellationToken));

    /// <summary>Phát hành mã QR check-in ngắn hạn, dùng một lần.</summary>
    /// <remarks>Mã cũ chưa dùng bị vô hiệu ngay khi phát hành mã mới. Mã thô chỉ trả về một lần, server chỉ lưu hash.</remarks>
    [HttpPost("{bookingId:guid}/check-in-token")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<CheckInTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CheckInTokenResponse>> IssueCheckInToken(Guid bookingId, CancellationToken cancellationToken) =>
        await Run(async () => await bookingService.IssueCheckInTokenAsync(CurrentUserId, bookingId, cancellationToken));

    /// <summary>Nội dung ảnh tiến độ của đơn tôi đặt; danh sách ảnh nằm trong chi tiết đơn.</summary>
    [HttpGet("{bookingId:guid}/photos/{photoId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPhotoContent(Guid bookingId, Guid photoId, CancellationToken cancellationToken)
    {
        var content = await bookingService.OpenPhotoAsync(CurrentUserId, bookingId, photoId, cancellationToken);
        if (content is null)
            return ToProblem(new BookingFlowException(StatusCodes.Status404NotFound, "BOOKING_PHOTO_NOT_FOUND", "Không tìm thấy ảnh."));
        return ImageFile(content);
    }
}
