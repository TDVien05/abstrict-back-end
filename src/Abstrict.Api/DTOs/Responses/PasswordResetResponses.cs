using System.Text.Json.Serialization;

namespace Abstrict.Api.DTOs.Responses;

/// <summary>
/// Kết quả sau khi tạo thử thách đặt lại mật khẩu.
/// </summary>
public sealed record ForgotPasswordResponse(
    Guid ChallengeId,
    string PhoneNumber,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset ResendAvailableAtUtc)
{
    /// <summary>
    /// Mã OTP ở môi trường phát triển (chỉ hiển thị khi cấu hình cho phép).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DevelopmentOtpCode { get; init; }
}

/// <summary>
/// Kết quả xác thực OTP đặt lại mật khẩu thành công.
/// </summary>
public sealed record VerifyResetOtpResponse(
    string ResetToken,
    string PhoneNumber);

/// <summary>
/// Kết quả sau khi đặt lại mật khẩu thành công.
/// </summary>
public sealed record ResetPasswordResponse(
    bool Success,
    string Message);
