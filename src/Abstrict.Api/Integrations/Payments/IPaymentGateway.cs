using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Integrations.Payments;

public sealed record PaymentInitiation(string ProviderName, string ProviderTransactionId, string? CheckoutUrl);

public interface IPaymentGateway
{
    Task<PaymentInitiation> CreateDepositAsync(Guid paymentId, long amountVnd, PaymentMethod method, CancellationToken cancellationToken);
    Task RefundAsync(string providerTransactionId, long amountVnd, CancellationToken cancellationToken);
}
