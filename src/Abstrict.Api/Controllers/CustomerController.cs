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

/// <summary>Tài khoản của khách hàng đang đăng nhập: hồ sơ, ảnh đại diện và sổ địa chỉ.</summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Customer))]
[Route(ApiRoutes.Version1 + "/customer")]
public sealed class CustomerController(
    ICustomerProfileService profileService,
    ICustomerAvatarService avatarService,
    ICustomerAddressService addressService) : ControllerBase
{
    #region Hồ sơ

    /// <summary>Hồ sơ của tôi kèm sổ địa chỉ (địa chỉ mặc định đứng đầu).</summary>
    /// <remarks>Thêm, sửa, xóa địa chỉ dùng <c>/customer/addresses</c>; gọi lại endpoint này để lấy hồ sơ mới nhất.</remarks>
    [HttpGet("profile")]
    [ProducesResponseType<CustomerProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerProfileResponse>> GetProfile(CancellationToken cancellationToken) =>
        Ok(await profileService.GetAsync(User.GetUserId(), cancellationToken));

    /// <summary>Tải lên hoặc thay ảnh đại diện.</summary>
    /// <remarks>
    /// Gửi <c>multipart/form-data</c> với trường <c>file</c> là ảnh JPG hoặc PNG, tối đa 5 MB.
    /// Định dạng được xác định từ nội dung file. Ảnh cũ bị xóa khi thay ảnh mới.
    /// </remarks>
    [HttpPut("profile/avatar")]
    [EnableRateLimiting("avatar-write")]
    [RequestSizeLimit(CustomerAvatarService.MaximumBytes + 256 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<CustomerAvatarResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<CustomerAvatarResponse>> UploadAvatar(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await avatarService.UploadAsync(User.GetUserId(), file.Length, stream, cancellationToken));
    }

    /// <summary>Lấy ảnh đại diện của tôi (nội dung ảnh).</summary>
    [HttpGet("profile/avatar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvatar(CancellationToken cancellationToken)
    {
        var content = await avatarService.OpenAsync(User.GetUserId(), cancellationToken)
            ?? throw new BookingFlowException(StatusCodes.Status404NotFound, "AVATAR_NOT_FOUND", "Chưa có ảnh đại diện.");

        Response.Headers.CacheControl = "private, no-cache";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(content.Stream, content.ContentType);
    }

    /// <summary>Xóa ảnh đại diện. Không có ảnh vẫn trả 204.</summary>
    [HttpDelete("profile/avatar")]
    [EnableRateLimiting("avatar-write")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAvatar(CancellationToken cancellationToken)
    {
        await avatarService.DeleteAsync(User.GetUserId(), cancellationToken);
        return NoContent();
    }

    #endregion

    #region Sổ địa chỉ

    /// <summary>Danh sách địa chỉ của tôi, địa chỉ mặc định đứng đầu.</summary>
    [HttpGet("addresses")]
    [ProducesResponseType<IReadOnlyList<CustomerAddressResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomerAddressResponse>>> ListAddresses(CancellationToken cancellationToken) =>
        Ok(await addressService.ListAsync(User.GetUserId(), cancellationToken));

    /// <summary>Thêm địa chỉ. Địa chỉ đầu tiên tự động là mặc định.</summary>
    [HttpPost("addresses")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<CustomerAddressResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerAddressResponse>> CreateAddress(SaveCustomerAddressRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await addressService.CreateAsync(User.GetUserId(), request, cancellationToken));

    /// <summary>Sửa địa chỉ.</summary>
    [HttpPut("addresses/{addressId:guid}")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<CustomerAddressResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerAddressResponse>> UpdateAddress(Guid addressId, SaveCustomerAddressRequest request, CancellationToken cancellationToken) =>
        Ok(await addressService.UpdateAsync(User.GetUserId(), addressId, request, cancellationToken));

    /// <summary>Xóa địa chỉ. Các đơn đã đặt vẫn giữ bản chụp địa chỉ.</summary>
    [HttpDelete("addresses/{addressId:guid}")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAddress(Guid addressId, CancellationToken cancellationToken)
    {
        await addressService.DeleteAsync(User.GetUserId(), addressId, cancellationToken);
        return NoContent();
    }

    #endregion
}
