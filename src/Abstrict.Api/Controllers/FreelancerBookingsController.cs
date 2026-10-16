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

/// <summary>Đơn đặt lịch phía freelancer: xem, xác nhận hoặc từ chối đơn đã có cọc.</summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Freelancer))]
[Route(ApiRoutes.Version1 + "/freelancer/bookings")]
public sealed class FreelancerBookingsController(IFreelancerBookingService bookingService) : BookingControllerBase
{
    /// <summary>Danh sách đơn gán cho tôi. Không gồm đơn chưa từng có cọc.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<FreelancerBookingSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<FreelancerBookingSummaryResponse>>> List(
        [FromQuery] BookingStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        await Run(async () => await bookingService.ListAsync(CurrentUserId, status, page, pageSize, cancellationToken));

    /// <summary>Số đơn theo từng trạng thái, dùng cho số trên các tab.</summary>
    [HttpGet("counts")]
    [ProducesResponseType<FreelancerBookingCountsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FreelancerBookingCountsResponse>> Counts(CancellationToken cancellationToken) =>
        await Run(async () => await bookingService.CountAsync(CurrentUserId, cancellationToken));

    /// <summary>Chi tiết đơn. Địa chỉ đầy đủ chỉ có sau khi đơn được xác nhận.</summary>
    [HttpGet("{bookingId:guid}")]
    [ProducesResponseType<FreelancerBookingDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FreelancerBookingDetailResponse>> Get(Guid bookingId, CancellationToken cancellationToken) =>
        await Run(async () => await bookingService.GetAsync(CurrentUserId, bookingId, cancellationToken));

    /// <summary>Xác nhận nhận đơn trước hạn phản hồi.</summary>
    [HttpPost("{bookingId:guid}/confirm")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<FreelancerBookingDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FreelancerBookingDetailResponse>> Confirm(Guid bookingId, CancellationToken cancellationToken) =>
        await Run(async () => await bookingService.ConfirmAsync(CurrentUserId, bookingId, cancellationToken));

    /// <summary>Từ chối đơn; khách được hoàn đủ cọc.</summary>
    [HttpPost("{bookingId:guid}/reject")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<FreelancerBookingDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FreelancerBookingDetailResponse>> Reject(Guid bookingId, RejectBookingRequest? request, CancellationToken cancellationToken) =>
        await Run(async () => await bookingService.RejectAsync(CurrentUserId, bookingId, request?.Reason, cancellationToken));

    /// <summary>Quét mã check-in của khách để bắt đầu ca; đơn chuyển sang CheckedIn.</summary>
    [HttpPost("{bookingId:guid}/check-in")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<FreelancerBookingDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FreelancerBookingDetailResponse>> CheckIn(Guid bookingId, CheckInRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await bookingService.CheckInAsync(CurrentUserId, bookingId, request.Token, cancellationToken));

    /// <summary>Đổi trạng thái một mục checklist trong ca (Pending/InProgress/Completed).</summary>
    [HttpPatch("{bookingId:guid}/tasks/{taskId:guid}")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<FreelancerBookingDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FreelancerBookingDetailResponse>> UpdateTaskStatus(
        Guid bookingId, Guid taskId, UpdateBookingTaskStatusRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await bookingService.UpdateTaskStatusAsync(CurrentUserId, bookingId, taskId, request.Status, cancellationToken));

    /// <summary>Hoàn tất ca; cần ít nhất một ảnh After, đơn chuyển sang PendingAcceptance.</summary>
    [HttpPost("{bookingId:guid}/complete")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<FreelancerBookingDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FreelancerBookingDetailResponse>> Complete(Guid bookingId, CancellationToken cancellationToken) =>
        await Run(async () => await bookingService.CompleteWorkAsync(CurrentUserId, bookingId, cancellationToken));

    /// <summary>Tải ảnh tiến độ (Before/After) trong ca; ảnh Before đầu tiên chuyển đơn sang InProgress.</summary>
    [HttpPost("{bookingId:guid}/photos")]
    [EnableRateLimiting("booking-write")]
    [RequestSizeLimit(KycImage.MaximumBytes + 256 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<FreelancerBookingDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<FreelancerBookingDetailResponse>> UploadPhoto(
        Guid bookingId, IFormFile file, [FromForm] BookingPhotoType type,
        [FromForm] Guid? bookingTaskId, [FromForm, StringLength(300)] string? caption, CancellationToken cancellationToken) =>
        await Run(async () =>
        {
            await using var stream = file.OpenReadStream();
            return await bookingService.UploadPhotoAsync(CurrentUserId, bookingId, type, bookingTaskId, caption, file.Length, stream, cancellationToken);
        });

    /// <summary>Nội dung ảnh tiến độ của đơn gán cho tôi.</summary>
    [HttpGet("{bookingId:guid}/photos/{photoId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPhotoContent(Guid bookingId, Guid photoId, CancellationToken cancellationToken)
    {
        try
        {
            var content = await bookingService.OpenPhotoAsync(CurrentUserId, bookingId, photoId, cancellationToken);
            if (content is null)
                return ToProblem(new BookingFlowException(StatusCodes.Status404NotFound, "BOOKING_PHOTO_NOT_FOUND", "Không tìm thấy ảnh."));
            return ImageFile(content);
        }
        catch (BookingFlowException exception)
        {
            return ToProblem(exception);
        }
    }
}
