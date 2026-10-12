using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;
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
}
