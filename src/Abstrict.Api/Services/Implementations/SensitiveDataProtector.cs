using Abstrict.Api.Common;
using Abstrict.Api.Options;
using Microsoft.Extensions.Options;

namespace Abstrict.Api.Services.Implementations;

public interface ISensitiveDataProtector
{
    string Protect(string plaintext);
    string? Unprotect(string protectedValue);
    string? Mask(string plaintext, int visibleTrailingDigits = 4);
}

public sealed class SensitiveDataProtector(IOptions<KycOptions> options) : ISensitiveDataProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly KycDataProtectionOptions _settings = options.Value.DataProtection;

    public string Protect(string plaintext)
    {
        var key = GetKey();
        var nonce = System.Security.Cryptography.RandomNumberGenerator.GetBytes(NonceSize);
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];
        using var aes = new System.Security.Cryptography.AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var payload = new byte[NonceSize + ciphertext.Length + TagSize];
        nonce.CopyTo(payload, 0);
        ciphertext.CopyTo(payload, NonceSize);
        tag.CopyTo(payload, NonceSize + ciphertext.Length);

        var version = string.IsNullOrWhiteSpace(_settings.EncryptionKeyVersion) ? "v1" : _settings.EncryptionKeyVersion;
        return $"{version}:{Convert.ToBase64String(payload)}";
    }

    public string? Unprotect(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
            return null;

        var separatorIndex = protectedValue.IndexOf(':');
        var payloadBase64 = separatorIndex >= 0 ? protectedValue[(separatorIndex + 1)..] : protectedValue;
        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(payloadBase64);
        }
        catch (FormatException)
        {
            return null;
        }

        if (payload.Length < NonceSize + TagSize)
            return null;

        try
        {
            var nonce = payload.AsSpan(0, NonceSize);
            var ciphertext = payload.AsSpan(NonceSize, payload.Length - NonceSize - TagSize);
            var tag = payload.AsSpan(payload.Length - TagSize, TagSize);
            var plaintext = new byte[ciphertext.Length];
            using var aes = new System.Security.Cryptography.AesGcm(GetKey(), TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return System.Text.Encoding.UTF8.GetString(plaintext);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    public string? Mask(string plaintext, int visibleTrailingDigits = 4)
    {
        if (string.IsNullOrEmpty(plaintext))
            return null;
        if (plaintext.Length <= visibleTrailingDigits)
            return new string('*', plaintext.Length);
        return new string('*', plaintext.Length - visibleTrailingDigits) + plaintext[^visibleTrailingDigits..];
    }

    private byte[] GetKey()
    {
        if (string.IsNullOrWhiteSpace(_settings.EncryptionKey))
            throw new ApiFlowException(StatusCodes.Status503ServiceUnavailable, "KYC_SECURITY_NOT_CONFIGURED", "Dịch vụ KYC chưa được cấu hình khóa mã hóa.");

        byte[] key;
        try
        {
            key = Convert.FromBase64String(_settings.EncryptionKey);
        }
        catch (FormatException)
        {
            throw new ApiFlowException(StatusCodes.Status503ServiceUnavailable, "KYC_SECURITY_NOT_CONFIGURED", "Khóa mã hóa KYC không hợp lệ.");
        }

        if (key.Length is not (16 or 24 or 32))
            throw new ApiFlowException(StatusCodes.Status503ServiceUnavailable, "KYC_SECURITY_NOT_CONFIGURED", "Khóa mã hóa KYC phải là AES-128/192/256.");
        return key;
    }
}
