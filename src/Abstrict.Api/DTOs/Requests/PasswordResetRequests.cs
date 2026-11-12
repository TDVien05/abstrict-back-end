using System.ComponentModel.DataAnnotations;

namespace Abstrict.Api.DTOs.Requests;

/// <summary>
/// Yêu cầu gửi mã OTP để đặt lại mật khẩu.
/// </summary>
public sealed record ForgotPasswordRequest
{
    /// <summary>
    /// Số điện thoại đã đăng ký tài khoản (định dạng Việt Nam).
    /// </summary>
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    public string PhoneNumber { get; init; } = string.Empty;
}

/// <summary>
/// Yêu cầu gửi lại mã OTP cho thử thách đặt lại mật khẩu.
/// </summary>
public sealed record ResendForgotPasswordOtpRequest
{
    /// <summary>
    /// Mã định danh thử thách OTP.
    /// </summary>
    [Required(ErrorMessage = "Thiếu mã định danh thử thách OTP.")]
    public Guid ChallengeId { get; init; }
}

/// <summary>
/// Yêu cầu xác thực mã OTP đặt lại mật khẩu.
/// </summary>
public sealed record VerifyResetOtpRequest
{
    /// <summary>
    /// Mã định danh thử thách OTP.
    /// </summary>
    [Required(ErrorMessage = "Thiếu mã định danh thử thách OTP.")]
    public Guid ChallengeId { get; init; }

    /// <summary>
    /// Mã OTP 6 chữ số.
    /// </summary>
    [Required(ErrorMessage = "Vui lòng nhập mã OTP.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có 6 chữ số.")]
    [RegularExpression(@"^[0-9]{6}$", ErrorMessage = "Mã OTP chỉ bao gồm 6 chữ số.")]
    public string Code { get; init; } = string.Empty;
}

/// <summary>
/// Yêu cầu đặt lại mật khẩu mới.
/// </summary>
public sealed record ResetPasswordRequest
{
    /// <summary>
    /// Mã xác thực cấp sau khi xác thực OTP thành công.
    /// </summary>
    public string? ResetToken { get; init; }

    /// <summary>
    /// Mã định danh thử thách OTP (khi gửi trực tiếp cùng OTP).
    /// </summary>
    public Guid? ChallengeId { get; init; }

    /// <summary>
    /// Mã OTP (khi gửi trực tiếp cùng thử thách).
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// Mật khẩu mới (tối thiểu 8 ký tự, gồm cả chữ và số).
    /// </summary>
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Mật khẩu phải có từ 8 đến 128 ký tự.")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Mật khẩu cần có cả chữ và số.")]
    public string NewPassword { get; init; } = string.Empty;

    /// <summary>
    /// Xác nhận mật khẩu mới.
    /// </summary>
    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới.")]
    public string ConfirmPassword { get; init; } = string.Empty;
}
