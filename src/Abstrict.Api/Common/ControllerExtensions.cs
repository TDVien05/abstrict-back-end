using System.Security.Claims;
using Abstrict.Api.Services.Implementations;
using Microsoft.AspNetCore.Mvc;

namespace Abstrict.Api.Common;

public static class ControllerExtensions
{
    /// <summary>Id người dùng trong token; ném 401 <c>INVALID_TOKEN</c> nếu token không có id hợp lệ.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new BookingFlowException(StatusCodes.Status401Unauthorized, "INVALID_TOKEN", "Phiên đăng nhập không hợp lệ.");

    /// <summary>Trả file riêng tư (ảnh, giấy tờ): không cho cache và không cho trình duyệt đoán kiểu nội dung.</summary>
    public static FileStreamResult PrivateFile(this ControllerBase controller, Stream stream, string contentType)
    {
        controller.Response.Headers.CacheControl = "private, no-store";
        controller.Response.Headers["X-Content-Type-Options"] = "nosniff";
        return controller.File(stream, contentType);
    }
}
