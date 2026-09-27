using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Integrations.Notifications;

public sealed class UnconfiguredPhoneOtpSender : IPhoneOtpSender
{
    public Task<string?> SendAsync(string phoneNumber, string code, OtpDeliveryChannel channel, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("A production SMS/Zalo OTP provider has not been configured.");
}
