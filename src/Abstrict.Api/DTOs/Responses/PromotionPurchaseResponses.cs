using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.DTOs.Responses;

/// <summary>
/// Một lượt mua gói. <c>Status</c> là trạng thái hiệu lực (lượt quá hạn là <c>Expired</c>); <c>IsCurrent</c> chỉ đúng với lượt đang chạy ngay lúc này.
/// <c>PaymentId</c> rỗng với gói miễn phí.
/// </summary>
public sealed record PromotionPurchaseResponse(
    Guid Id,
    Guid PlanId,
    string PlanCode,
    string PlanName,
    int DurationDays,
    long PriceVnd,
    PromotionPurchaseStatus Status,
    bool IsCurrent,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    Guid? PaymentId,
    PaymentMethod? PaymentMethod,
    DateTimeOffset CreatedAtUtc);
