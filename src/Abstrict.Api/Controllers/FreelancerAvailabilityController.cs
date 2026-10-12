using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Abstrict.Api.Controllers;

/// <summary>Freelancer đã duyệt khai báo các khoảng rảnh để khách đặt lịch.</summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Freelancer))]
[Route(ApiRoutes.Version1 + "/freelancer/availability")]
public sealed class FreelancerAvailabilityController(IAvailabilityService availabilityService) : BookingControllerBase
{
    /// <summary>Các khoảng rảnh của tôi từ ngày <c>from</c> (giờ Việt Nam, mặc định hôm nay).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AvailabilityWindowResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AvailabilityWindowResponse>>> List(
        [FromQuery] DateOnly? from, [FromQuery] int days = 14, CancellationToken cancellationToken = default) =>
        await Run(async () => await availabilityService.ListMineAsync(CurrentUserId, from, days, cancellationToken));

    /// <summary>Thêm khoảng rảnh: theo bước 30 phút, tối thiểu 2 giờ, ở tương lai và không chồng khoảng khác.</summary>
    [HttpPost]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<AvailabilityWindowResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AvailabilityWindowResponse>> Create(CreateAvailabilityWindowRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await availabilityService.CreateAsync(CurrentUserId, request, cancellationToken), StatusCodes.Status201Created);

    /// <summary>Xóa khoảng rảnh chưa có đơn đặt chồng lên.</summary>
    [HttpDelete("{windowId:guid}")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid windowId, CancellationToken cancellationToken) =>
        await RunNoContent(async () => await availabilityService.DeleteAsync(CurrentUserId, windowId, cancellationToken));
}
