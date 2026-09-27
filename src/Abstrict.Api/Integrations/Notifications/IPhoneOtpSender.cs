using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Integrations.Notifications;

public interface IPhoneOtpSender
{
    Task<string?> SendAsync(string phoneNumber, string code, OtpDeliveryChannel channel, CancellationToken cancellationToken);
}
