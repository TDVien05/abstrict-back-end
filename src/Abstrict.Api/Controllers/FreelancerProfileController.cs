using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Abstrict.Api.Controllers;

/// <summary>Freelancer tự quản lý hồ sơ công khai: giới thiệu, kỹ năng (dịch vụ và giá theo giờ), khu vực nhận ca và trạng thái nhận đơn.</summary>
/// <remarks>Mọi thao tác ghi trả về hồ sơ mới nhất. Dữ liệu định danh (họ tên khai sinh, CCCD, ngày sinh, địa chỉ) chỉ sửa qua API KYC.</remarks>
[ApiController]
[Authorize(Roles = nameof(UserRole.Freelancer))]
[Route(ApiRoutes.Version1 + "/freelancer/profile")]
public sealed class FreelancerProfileController(IFreelancerProfileService profileService) : BookingControllerBase
{
    /// <summary>Hồ sơ của tôi, gồm cả dịch vụ đang tắt và điều kiện còn thiếu để nhận đơn.</summary>
    [HttpGet]
    [ProducesResponseType<MyFreelancerProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MyFreelancerProfileResponse>> Get(CancellationToken cancellationToken) =>
        await Run(async () => await profileService.GetAsync(CurrentUserId, cancellationToken));

    /// <summary>Cập nhật giới thiệu, bật/tắt nhận đơn và tự động nhận đơn. Trường không gửi được giữ nguyên.</summary>
    /// <remarks>Bật nhận đơn cần hồ sơ đã duyệt, ít nhất một dịch vụ đang bật và một khu vực phục vụ (<c>PROFILE_NOT_READY</c>).</remarks>
    [HttpPatch]
    [EnableRateLimiting("profile-write")]
    [ProducesResponseType<MyFreelancerProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MyFreelancerProfileResponse>> Update(UpdateFreelancerProfileRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await profileService.UpdateAsync(CurrentUserId, request, cancellationToken));

    /// <summary>Thêm hoặc sửa một kỹ năng (dịch vụ) cùng giá theo giờ; <c>serviceCategoryId</c> lấy từ <c>GET /catalog/service-categories</c>.</summary>
    [HttpPut("services/{serviceCategoryId:guid}")]
    [EnableRateLimiting("profile-write")]
    [ProducesResponseType<MyFreelancerProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MyFreelancerProfileResponse>> SaveService(
        Guid serviceCategoryId, SaveFreelancerServiceRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await profileService.SaveServiceAsync(CurrentUserId, serviceCategoryId, request, cancellationToken));

    /// <summary>Xóa một kỹ năng. Đơn đã đặt không bị ảnh hưởng; nếu không còn dịch vụ nào đang bật, nhận đơn được tự tắt.</summary>
    [HttpDelete("services/{serviceCategoryId:guid}")]
    [EnableRateLimiting("profile-write")]
    [ProducesResponseType<MyFreelancerProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MyFreelancerProfileResponse>> RemoveService(Guid serviceCategoryId, CancellationToken cancellationToken) =>
        await Run(async () => await profileService.RemoveServiceAsync(CurrentUserId, serviceCategoryId, cancellationToken));

    /// <summary>Thay toàn bộ danh sách khu vực nhận ca (1–20 khu vực từ <c>/catalog/service-areas</c>, kèm bán kính di chuyển 1–100 km, mặc định 10).</summary>
    [HttpPut("service-areas")]
    [EnableRateLimiting("profile-write")]
    [ProducesResponseType<MyFreelancerProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MyFreelancerProfileResponse>> ReplaceServiceAreas(
        ReplaceFreelancerServiceAreasRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await profileService.ReplaceServiceAreasAsync(CurrentUserId, request, cancellationToken));
}
