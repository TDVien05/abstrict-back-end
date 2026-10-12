using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Services.Interfaces;

public interface IBookingPaymentService
{
    Task<DepositPaymentResponse> InitiateDepositAsync(Guid userId, Guid bookingId, string? idempotencyKey, PaymentMethod method, CancellationToken cancellationToken);

    /// <summary>Chốt kết quả một giao dịch sandbox; chỉ người trả tiền được gọi và chỉ khi sandbox được bật.</summary>
    Task<DepositPaymentResponse> ConfirmSandboxAsync(Guid userId, Guid paymentId, bool succeeded, CancellationToken cancellationToken);

    /// <summary>Xử lý kết quả từ cổng thanh toán (webhook). Idempotent: gọi lặp không đổi trạng thái đã chốt.</summary>
    Task HandleGatewayResultAsync(string providerName, string providerTransactionId, bool succeeded, CancellationToken cancellationToken);
}
