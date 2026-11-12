using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;

namespace Abstrict.Api.Services.Interfaces;

/// <summary>
/// Dịch vụ xử lý quy trình quên và đặt lại mật khẩu.
/// </summary>
public interface IPasswordResetService
{
    /// <summary>
    /// Tiếp nhận yêu cầu đặt lại mật khẩu và gửi OTP xác thực.
    /// </summary>
    Task<ForgotPasswordResponse> RequestResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Gửi lại mã OTP cho thử thách đặt lại mật khẩu.
    /// </summary>
    Task<ForgotPasswordResponse?> ResendResetOtpAsync(ResendForgotPasswordOtpRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Xác thực mã OTP và cấp mã xác thực đặt lại mật khẩu (reset token).
    /// </summary>
    Task<VerifyResetOtpResponse> VerifyResetOtpAsync(VerifyResetOtpRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Đặt lại mật khẩu mới cho tài khoản.
    /// </summary>
    Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);
}
