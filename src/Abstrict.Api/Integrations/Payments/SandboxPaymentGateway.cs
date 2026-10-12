using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Integrations.Payments;

/// <summary>Cổng giả lập: không gọi ra ngoài, kết quả được chốt qua endpoint sandbox.</summary>
public sealed class SandboxPaymentGateway : IPaymentGateway
{
    public const string Name = "sandbox";

    public Task<PaymentInitiation> CreateDepositAsync(Guid paymentId, long amountVnd, PaymentMethod method, CancellationToken cancellationToken) =>
        Task.FromResult(new PaymentInitiation(Name, Guid.NewGuid().ToString("N"), null));

    public Task RefundAsync(string providerTransactionId, long amountVnd, CancellationToken cancellationToken) => Task.CompletedTask;
}
