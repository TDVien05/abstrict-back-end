using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Services.Interfaces;

public interface IPromotionPurchaseService
{
    /// <summary>
    /// Mua gói. Gói giá 0 kích hoạt ngay; gói có phí tạo giao dịch chờ thanh toán (gọi lại cùng <c>Idempotency-Key</c> hoặc cùng gói đang chờ thì trả lại lượt cũ).
    /// </summary>
    Task<PromotionPurchaseResponse> PurchaseAsync(Guid userId, Guid planId, PaymentMethod method, string? idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Chốt kết quả giao dịch sandbox của lượt mua; trả 404 khi sandbox tắt.</summary>
    Task<PromotionPurchaseResponse> ConfirmSandboxAsync(Guid userId, Guid purchaseId, bool succeeded, CancellationToken cancellationToken);

    /// <summary>Lịch sử mua của đối tác đang đăng nhập, mới nhất trước.</summary>
    Task<IReadOnlyList<PromotionPurchaseResponse>> ListAsync(Guid userId, CancellationToken cancellationToken);
}
