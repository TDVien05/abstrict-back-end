using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Implementations;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Abstrict.Api.Controllers;

/// <summary>Hồ sơ của khách hàng đang đăng nhập.</summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Customer))]
[Route(ApiRoutes.Version1 + "/customer/profile")]
public sealed class CustomerProfileController(ICustomerAvatarService avatarService, ICustomerProfileService profileService) : BookingControllerBase
{
    /// <summary>Hồ sơ của tôi kèm sổ địa chỉ (địa chỉ mặc định đứng đầu).</summary>
    /// <remarks>Thêm, sửa, xóa địa chỉ dùng <c>/customer/addresses</c>; gọi lại endpoint này để lấy hồ sơ mới nhất.</remarks>
    [HttpGet]
    [ProducesResponseType<CustomerProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerProfileResponse>> Get(CancellationToken cancellationToken) =>
        await Run(async () => await profileService.GetAsync(CurrentUserId, cancellationToken));

    /// <summary>Tải lên hoặc thay ảnh đại diện.</summary>
    /// <remarks>
    /// Gửi <c>multipart/form-data</c> với trường <c>file</c> là ảnh JPG hoặc PNG, tối đa 5 MB.
    /// Định dạng được xác định từ nội dung file. Ảnh cũ bị xóa khi thay ảnh mới.
    /// </remarks>
    [HttpPut("avatar")]
    [EnableRateLimiting("avatar-write")]
    [RequestSizeLimit(CustomerAvatarService.MaximumBytes + 256 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<CustomerAvatarResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<CustomerAvatarResponse>> UploadAvatar(IFormFile file, CancellationToken cancellationToken) =>
        await Run(async () =>
        {
            await using var stream = file.OpenReadStream();
            return await avatarService.UploadAsync(CurrentUserId, file.Length, stream, cancellationToken);
        });

    /// <summary>Lấy ảnh đại diện của tôi (nội dung ảnh).</summary>
    [HttpGet("avatar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvatar(CancellationToken cancellationToken)
    {
        try
        {
            var content = await avatarService.OpenAsync(CurrentUserId, cancellationToken);
            if (content is null)
                return ToProblem(new BookingFlowException(StatusCodes.Status404NotFound, "AVATAR_NOT_FOUND", "Chưa có ảnh đại diện."));

            Response.Headers.CacheControl = "private, no-cache";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(content.Stream, content.ContentType);
        }
        catch (BookingFlowException exception)
        {
            return ToProblem(exception);
        }
    }

    /// <summary>Xóa ảnh đại diện. Không có ảnh vẫn trả 204.</summary>
    [HttpDelete("avatar")]
    [EnableRateLimiting("avatar-write")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAvatar(CancellationToken cancellationToken) =>
        await RunNoContent(async () => await avatarService.DeleteAsync(CurrentUserId, cancellationToken));
}
