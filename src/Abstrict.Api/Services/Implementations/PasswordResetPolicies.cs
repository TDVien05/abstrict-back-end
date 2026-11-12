using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Abstrict.Api.Models.Entities;

namespace Abstrict.Api.Services.Implementations;

/// <summary>
/// Bộ kiểm tra tính hợp lệ dữ liệu đầu vào cho quy trình đặt lại mật khẩu.
/// </summary>
public static class PasswordResetInputValidator
{
    private static readonly Regex StrongPasswordPattern = new(@"^(?=.*[A-Za-z])(?=.*\d).+$", RegexOptions.Compiled);

    /// <summary>
    /// Kiểm tra và chuẩn hóa số điện thoại Việt Nam.
    /// </summary>
    public static string ValidatePhoneNumber(string? input)
    {
        var normalized = VietnamesePhoneNumber.Normalize(input ?? string.Empty);
        if (normalized is null)
            throw new AuthFlowException(StatusCodes.Status400BadRequest, "INVALID_PHONE_NUMBER", "Số điện thoại Việt Nam phải có 10 hoặc 11 chữ số.");

        return normalized;
    }

    /// <summary>
    /// Kiểm tra mật khẩu mới và mật khẩu xác nhận.
    /// </summary>
    public static void ValidatePassword(string? newPassword, string? confirmPassword)
    {
        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            throw new AuthFlowException(StatusCodes.Status400BadRequest, "PASSWORD_CONFIRMATION_MISMATCH", "Mật khẩu xác nhận không khớp.");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8 || !StrongPasswordPattern.IsMatch(newPassword))
            throw new AuthFlowException(StatusCodes.Status400BadRequest, "WEAK_PASSWORD", "Mật khẩu cần có ít nhất 8 ký tự, bao gồm cả chữ và số.");
    }
}

/// <summary>
/// Trình tạo và xác thực token đặt lại mật khẩu an toàn (stateless HMAC token).
/// </summary>
public sealed class PasswordResetTokenProvider
{
    private readonly byte[] _keyBytes;

    public PasswordResetTokenProvider(string? key)
    {
        var rawKey = !string.IsNullOrWhiteSpace(key) && Encoding.UTF8.GetByteCount(key) >= 32
            ? key
            : "ABSTRICT-fallback-password-reset-key-min-32-bytes-2026";
        _keyBytes = Encoding.UTF8.GetBytes(rawKey);
    }

    /// <summary>
    /// Tạo token đặt lại mật khẩu kèm chữ ký số HMAC-SHA256.
    /// </summary>
    public string GenerateResetToken(Guid userId, Guid challengeId, DateTimeOffset expiresAtUtc)
    {
        var payload = $"PR1.{userId:N}.{challengeId:N}.{expiresAtUtc.ToUnixTimeSeconds()}";
        var signature = HMACSHA256.HashData(_keyBytes, Encoding.UTF8.GetBytes(payload));
        var base64UrlSignature = Convert.ToBase64String(signature)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return $"{payload}.{base64UrlSignature}";
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ và thời hạn của token đặt lại mật khẩu.
    /// </summary>
    public bool ValidateResetToken(string? token, DateTimeOffset now, out Guid userId, out Guid challengeId)
    {
        userId = Guid.Empty;
        challengeId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        var parts = token.Split('.');
        if (parts.Length != 5 || parts[0] != "PR1")
            return false;

        var payload = $"{parts[0]}.{parts[1]}.{parts[2]}.{parts[3]}";
        var expectedSignature = HMACSHA256.HashData(_keyBytes, Encoding.UTF8.GetBytes(payload));

        byte[] actualSignature;
        try
        {
            var base64 = parts[4].Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            actualSignature = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return false;
        }

        if (!CryptographicOperations.FixedTimeEquals(expectedSignature, actualSignature))
            return false;

        if (!Guid.TryParseExact(parts[1], "N", out userId) || !Guid.TryParseExact(parts[2], "N", out challengeId))
            return false;

        if (!long.TryParse(parts[3], out var expiresSeconds))
            return false;

        return expiresSeconds >= now.ToUnixTimeSeconds();
    }
}
