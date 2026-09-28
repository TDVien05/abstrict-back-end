using System.Security.Cryptography;
using System.Text;
using Abstrict.Api.Common;
using Abstrict.Api.Options;
using Microsoft.Extensions.Options;

namespace Abstrict.Api.Services.Implementations;

public interface IIdentityFingerprintService
{
    string Compute(string documentNumber);
}

public sealed class IdentityFingerprintService(IOptions<KycOptions> options) : IIdentityFingerprintService
{
    private readonly string _key = options.Value.DataProtection.IdentityHmacKey;

    public string Compute(string documentNumber)
    {
        if (string.IsNullOrWhiteSpace(_key) || Encoding.UTF8.GetByteCount(_key) < 32)
            throw new ApiFlowException(StatusCodes.Status503ServiceUnavailable, "KYC_SECURITY_NOT_CONFIGURED", "Dịch vụ KYC chưa được cấu hình khóa tra cứu danh tính.");

        var normalized = documentNumber.Trim();
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(_key), Encoding.UTF8.GetBytes(normalized)));
    }
}
