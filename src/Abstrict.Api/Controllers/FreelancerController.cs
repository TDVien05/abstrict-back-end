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

/// <summary>Mọi thao tác của freelancer đang đăng nhập: hồ sơ công khai, KYC, lịch rảnh và đơn được gán.</summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Freelancer))]
[Route(ApiRoutes.Version1 + "/freelancer")]
public sealed class FreelancerController(
    IFreelancerProfileService profileService,
    IFreelancerKycService kycService,
    IAvailabilityService availabilityService,
    IFreelancerBookingService bookingService) : ControllerBase
{
    #region Hồ sơ công khai
    // Giới thiệu, kỹ năng (dịch vụ và giá theo giờ), khu vực nhận ca và trạng thái nhận đơn.
    // Mọi thao tác ghi trả về hồ sơ mới nhất. Dữ liệu định danh (họ tên khai sinh, CCCD, ngày sinh, địa chỉ) chỉ sửa qua API KYC.

    /// <summary>Hồ sơ của tôi, gồm cả dịch vụ đang tắt và điều kiện còn thiếu để nhận đơn.</summary>
    [HttpGet("profile")]
    [ProducesResponseType<MyFreelancerProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MyFreelancerProfileResponse>> GetProfile(CancellationToken cancellationToken) =>
        Ok(await profileService.GetAsync(User.GetUserId(), cancellationToken));

    /// <summary>Cập nhật giới thiệu, bật/tắt nhận đơn và tự động nhận đơn. Trường không gửi được giữ nguyên.</summary>
    /// <remarks>Bật nhận đơn cần hồ sơ đã duyệt, ít nhất một dịch vụ đang bật và một khu vực phục vụ (<c>PROFILE_NOT_READY</c>).</remarks>
    [HttpPatch("profile")]
    [EnableRateLimiting("profile-write")]
    [ProducesResponseType<MyFreelancerProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MyFreelancerProfileResponse>> UpdateProfile(UpdateFreelancerProfileRequest request, CancellationToken cancellationToken) =>
        Ok(await profileService.UpdateAsync(User.GetUserId(), request, cancellationToken));

    /// <summary>Thêm hoặc sửa một kỹ năng (dịch vụ) cùng giá theo giờ; <c>serviceCategoryId</c> lấy từ <c>GET /catalog/service-categories</c>.</summary>
    [HttpPut("profile/services/{serviceCategoryId:guid}")]
    [EnableRateLimiting("profile-write")]
    [ProducesResponseType<MyFreelancerProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MyFreelancerProfileResponse>> SaveService(
        Guid serviceCategoryId, SaveFreelancerServiceRequest request, CancellationToken cancellationToken) =>
        Ok(await profileService.SaveServiceAsync(User.GetUserId(), serviceCategoryId, request, cancellationToken));

    /// <summary>Xóa một kỹ năng. Đơn đã đặt không bị ảnh hưởng; nếu không còn dịch vụ nào đang bật, nhận đơn được tự tắt.</summary>
    [HttpDelete("profile/services/{serviceCategoryId:guid}")]
    [EnableRateLimiting("profile-write")]
    [ProducesResponseType<MyFreelancerProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MyFreelancerProfileResponse>> RemoveService(Guid serviceCategoryId, CancellationToken cancellationToken) =>
        Ok(await profileService.RemoveServiceAsync(User.GetUserId(), serviceCategoryId, cancellationToken));

    /// <summary>Thay toàn bộ danh sách khu vực nhận ca (1–20 khu vực từ <c>/catalog/service-areas</c>, kèm bán kính di chuyển 1–100 km, mặc định 10).</summary>
    [HttpPut("profile/service-areas")]
    [EnableRateLimiting("profile-write")]
    [ProducesResponseType<MyFreelancerProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MyFreelancerProfileResponse>> ReplaceServiceAreas(
        ReplaceFreelancerServiceAreasRequest request, CancellationToken cancellationToken) =>
        Ok(await profileService.ReplaceServiceAreasAsync(User.GetUserId(), request, cancellationToken));

    #endregion

    #region KYC

    [HttpGet("kyc")]
    public async Task<ActionResult<FreelancerKycResponse>> GetKyc(CancellationToken cancellationToken) =>
        Ok(await kycService.GetAsync(User.GetUserId(), cancellationToken));

    [HttpPut("kyc/profile")]
    [EnableRateLimiting("kyc-write")]
    public async Task<ActionResult<FreelancerKycResponse>> SaveKycProfile(SaveFreelancerKycProfileRequest request, CancellationToken cancellationToken) =>
        Ok(await kycService.SaveProfileAsync(User.GetUserId(), request, cancellationToken));

    [HttpPut("kyc/documents/{type}")]
    [EnableRateLimiting("kyc-write")]
    [RequestSizeLimit(KycImage.MaximumBytes + 256 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<KycDocumentResponse>> UploadKycDocument(
        VerificationDocumentType type, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await kycService.UploadDocumentAsync(User.GetUserId(), type, file.FileName, file.Length, stream, cancellationToken));
    }

    [HttpPost("kyc/submit")]
    [EnableRateLimiting("kyc-write")]
    public async Task<ActionResult<FreelancerKycResponse>> SubmitKyc(CancellationToken cancellationToken) =>
        Ok(await kycService.SubmitAsync(User.GetUserId(), cancellationToken));

    [HttpGet("kyc/documents/{documentId:guid}/content")]
    public async Task<IActionResult> GetKycDocumentContent(Guid documentId, CancellationToken cancellationToken)
    {
        var content = await kycService.OpenDocumentAsync(User.GetUserId(), documentId, cancellationToken);
        return content is null ? NotFound() : this.PrivateFile(content.Stream, content.ContentType);
    }

    #endregion

    #region Lịch rảnh
    // Freelancer đã duyệt khai báo các khoảng rảnh để khách đặt lịch.

    /// <summary>Các khoảng rảnh của tôi từ ngày <c>from</c> (giờ Việt Nam, mặc định hôm nay).</summary>
    [HttpGet("availability")]
    [ProducesResponseType<IReadOnlyList<AvailabilityWindowResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AvailabilityWindowResponse>>> ListAvailability(
        [FromQuery] DateOnly? from, [FromQuery] int days = 14, CancellationToken cancellationToken = default) =>
        Ok(await availabilityService.ListMineAsync(User.GetUserId(), from, days, cancellationToken));

    /// <summary>Thêm khoảng rảnh: theo bước 30 phút, tối thiểu 2 giờ, ở tương lai và không chồng khoảng khác.</summary>
    [HttpPost("availability")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<AvailabilityWindowResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AvailabilityWindowResponse>> CreateAvailability(CreateAvailabilityWindowRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await availabilityService.CreateAsync(User.GetUserId(), request, cancellationToken));

    /// <summary>Xóa khoảng rảnh chưa có đơn đặt chồng lên.</summary>
    [HttpDelete("availability/{windowId:guid}")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAvailability(Guid windowId, CancellationToken cancellationToken)
    {
        await availabilityService.DeleteAsync(User.GetUserId(), windowId, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Đơn đặt lịch
    // Xem, xác nhận hoặc từ chối đơn đã có cọc; check-in, checklist, ảnh tiến độ và hoàn tất ca.

    /// <summary>Danh sách đơn gán cho tôi. Không gồm đơn chưa từng có cọc.</summary>
    [HttpGet("bookings")]
    [ProducesResponseType<PagedResponse<FreelancerBookingSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<FreelancerBookingSummaryResponse>>> ListBookings(
        [FromQuery] BookingStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await bookingService.ListAsync(User.GetUserId(), status, page, pageSize, cancellationToken));

    /// <summary>Số đơn theo từng trạng thái, dùng cho số trên các tab.</summary>
    [HttpGet("bookings/counts")]
    [ProducesResponseType<FreelancerBookingCountsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FreelancerBookingCountsResponse>> CountBookings(CancellationToken cancellationToken) =>
        Ok(await bookingService.CountAsync(User.GetUserId(), cancellationToken));

    /// <summary>Chi tiết đơn. Địa chỉ đầy đủ chỉ có sau khi đơn được xác nhận.</summary>
    [HttpGet("bookings/{bookingId:guid}")]
    [ProducesResponseType<FreelancerBookingDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FreelancerBookingDetailResponse>> GetBooking(Guid bookingId, CancellationToken cancellationToken) =>
        Ok(await bookingService.GetAsync(User.GetUserId(), bookingId, cancellationToken));

    /// <summary>Xác nhận nhận đơn trước hạn phản hồi.</summary>
    [HttpPost("bookings/{bookingId:guid}/confirm")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<FreelancerBookingDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FreelancerBookingDetailResponse>> ConfirmBooking(Guid bookingId, CancellationToken cancellationToken) =>
        Ok(await bookingService.ConfirmAsync(User.GetUserId(), bookingId, cancellationToken));

    /// <summary>Từ chối đơn; khách được hoàn đủ cọc.</summary>
    [HttpPost("bookings/{bookingId:guid}/reject")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<FreelancerBookingDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FreelancerBookingDetailResponse>> RejectBooking(Guid bookingId, RejectBookingRequest? request, CancellationToken cancellationToken) =>
        Ok(await bookingService.RejectAsync(User.GetUserId(), bookingId, request?.Reason, cancellationToken));

    /// <summary>Quét mã check-in của khách để bắt đầu ca; đơn chuyển sang CheckedIn.</summary>
    [HttpPost("bookings/{bookingId:guid}/check-in")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<FreelancerBookingDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FreelancerBookingDetailResponse>> CheckIn(Guid bookingId, CheckInRequest request, CancellationToken cancellationToken) =>
        Ok(await bookingService.CheckInAsync(User.GetUserId(), bookingId, request.Token, cancellationToken));

    /// <summary>Đổi trạng thái một mục checklist trong ca (Pending/InProgress/Completed).</summary>
    [HttpPatch("bookings/{bookingId:guid}/tasks/{taskId:guid}")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<FreelancerBookingDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FreelancerBookingDetailResponse>> UpdateTaskStatus(
        Guid bookingId, Guid taskId, UpdateBookingTaskStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await bookingService.UpdateTaskStatusAsync(User.GetUserId(), bookingId, taskId, request.Status, cancellationToken));

    /// <summary>Hoàn tất ca; cần ít nhất một ảnh After, đơn chuyển sang PendingAcceptance.</summary>
    [HttpPost("bookings/{bookingId:guid}/complete")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<FreelancerBookingDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FreelancerBookingDetailResponse>> CompleteBooking(Guid bookingId, CancellationToken cancellationToken) =>
        Ok(await bookingService.CompleteWorkAsync(User.GetUserId(), bookingId, cancellationToken));

    /// <summary>Tải ảnh tiến độ (Before/After) trong ca; ảnh Before đầu tiên chuyển đơn sang InProgress.</summary>
    [HttpPost("bookings/{bookingId:guid}/photos")]
    [EnableRateLimiting("booking-write")]
    [RequestSizeLimit(KycImage.MaximumBytes + 256 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<FreelancerBookingDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<FreelancerBookingDetailResponse>> UploadBookingPhoto(
        Guid bookingId, IFormFile file, [FromForm] BookingPhotoType type,
        [FromForm] Guid? bookingTaskId, [FromForm, StringLength(300)] string? caption, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await bookingService.UploadPhotoAsync(User.GetUserId(), bookingId, type, bookingTaskId, caption, file.Length, stream, cancellationToken));
    }

    /// <summary>Nội dung ảnh tiến độ của đơn gán cho tôi.</summary>
    [HttpGet("bookings/{bookingId:guid}/photos/{photoId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBookingPhotoContent(Guid bookingId, Guid photoId, CancellationToken cancellationToken)
    {
        var content = await bookingService.OpenPhotoAsync(User.GetUserId(), bookingId, photoId, cancellationToken)
            ?? throw new BookingFlowException(StatusCodes.Status404NotFound, "BOOKING_PHOTO_NOT_FOUND", "Không tìm thấy ảnh.");
        return this.PrivateFile(content.Stream, content.ContentType);
    }

    #endregion
}
