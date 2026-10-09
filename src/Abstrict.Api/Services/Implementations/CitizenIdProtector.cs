using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace Abstrict.Api.Services.Implementations;

public sealed record ProtectedCitizenId(string Protected, string Hash, string Last4);

/// <summary>Mã hóa số CCCD khi lưu và tạo hash xác định để kiểm tra trùng.</summary>
public sealed class CitizenIdProtector(IDataProtectionProvider dataProtectionProvider, string? hashKey)
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("Abstrict.Kyc.CitizenId.v1");

    public ProtectedCitizenId Protect(string citizenIdNumber)
    {
        if (string.IsNullOrWhiteSpace(hashKey) || Encoding.UTF8.GetByteCount(hashKey) < 32)
            throw new KycFlowException(StatusCodes.Status503ServiceUnavailable, "KYC_SECURITY_NOT_CONFIGURED", "Dịch vụ xác minh danh tính chưa được cấu hình.");

        var hash = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(hashKey), Encoding.UTF8.GetBytes(citizenIdNumber)));
        return new ProtectedCitizenId(_protector.Protect(citizenIdNumber), hash, citizenIdNumber[^4..]);
    }

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}

public sealed class KycFlowException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
