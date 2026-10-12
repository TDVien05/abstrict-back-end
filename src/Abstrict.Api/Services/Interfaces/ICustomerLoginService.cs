using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;

namespace Abstrict.Api.Services.Interfaces;

/// <summary>Kết quả đăng nhập kèm refresh token thô để controller đặt vào cookie HttpOnly; token thô không nằm trong body.</summary>
public sealed record LoginResult(CustomerLoginResponse Response, string RefreshToken, DateTimeOffset RefreshExpiresAtUtc);

public interface ICustomerLoginService
{
    Task<LoginResult?> LoginAsync(CustomerLoginRequest request, CancellationToken cancellationToken);

    /// <summary>Cấp access token mới từ refresh token còn hiệu lực; <c>null</c> nếu phiên không hợp lệ.</summary>
    Task<LoginResult?> RefreshAsync(string? refreshToken, CancellationToken cancellationToken);

    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);
}
