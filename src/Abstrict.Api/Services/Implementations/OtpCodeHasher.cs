using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;

namespace Abstrict.Api.Services.Implementations;

public sealed class OtpCodeHasher(string? key)
{
    public byte[] Hash(string code)
    {
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
            throw new AuthFlowException(StatusCodes.Status503ServiceUnavailable, "OTP_SECURITY_NOT_CONFIGURED", "Dịch vụ xác thực chưa được cấu hình.");

        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(code));
    }

    public bool Verify(string code, string storedHash)
    {
        byte[] actualHash;
        try
        {
            actualHash = Convert.FromBase64String(storedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Hash(code), actualHash);
    }
}

public static class OtpAttemptPolicy
{
    public static bool RecordFailure(Abstrict.Api.Models.Entities.PhoneOtpChallenge challenge, DateTimeOffset now)
    {
        challenge.FailedAttemptCount++;
        if (challenge.FailedAttemptCount < CustomerRegistrationService.MaximumOtpAttempts)
            return false;

        challenge.LockedAtUtc = now;
        challenge.ConsumedAtUtc = now;
        return true;
    }
}

public static class OtpResponseExposurePolicy
{
    public static bool ShouldExposeCode(string environmentName, bool configured) =>
        configured && string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase);
}
