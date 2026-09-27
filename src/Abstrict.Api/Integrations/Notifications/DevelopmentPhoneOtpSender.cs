using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Integrations.Notifications;

public sealed class DevelopmentPhoneOtpSender : IPhoneOtpSender
{
    public Task<string?> SendAsync(string phoneNumber, string code, OtpDeliveryChannel channel, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<string?>(code);
    }
}
