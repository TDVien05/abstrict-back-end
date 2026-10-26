using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.DTOs.Responses;

/// <summary><c>PurchaseCount</c> là số lượt mua đã ghi nhận; gói đã có lượt mua không xóa được, chỉ tắt.</summary>
public sealed record AdminPromotionPlanResponse(
    Guid Id,
    string Code,
    string Name,
    PromotionAudience Audience,
    int DurationDays,
    long PriceVnd,
    int? PriorityRadiusKm,
    int? WorkerLimit,
    IReadOnlyList<string> Benefits,
    bool IsActive,
    int PurchaseCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record PromotionDailyReportRow(string Date, int Purchases, long RevenueVnd);

public sealed record PromotionPlanReportRow(
    Guid PlanId, string Code, string Name, PromotionAudience Audience, bool IsActive, int Purchases, int ActiveNow, long RevenueVnd);

public sealed record PromotionAudienceReportRow(PromotionAudience Audience, int Purchases, long RevenueVnd);

/// <summary>
/// Báo cáo gói cho admin. Lượt mua "đã thanh toán" là lượt Active (kể cả đã hết hạn) và gồm cả gói miễn phí; doanh thu cộng giá đã chốt lúc mua.
/// <c>Daily</c> luôn đủ 7 ngày gần nhất theo giờ Việt Nam.
/// </summary>
public sealed record PromotionReportResponse(
    DateTimeOffset GeneratedAtUtc,
    int ActivePlanCount,
    int InactivePlanCount,
    int PaidPurchases,
    int ActiveNow,
    int PendingPayment,
    int PaymentFailed,
    long TotalRevenueVnd,
    long Last7DaysRevenueVnd,
    IReadOnlyList<PromotionDailyReportRow> Daily,
    IReadOnlyList<PromotionAudienceReportRow> Audiences,
    IReadOnlyList<PromotionPlanReportRow> Plans);

/// <summary>Gói đang bán cho đúng đối tượng của đối tác (freelancer hoặc doanh nghiệp).</summary>
public sealed record PartnerPromotionPlanResponse(
    Guid Id,
    string Code,
    string Name,
    PromotionAudience Audience,
    int DurationDays,
    long PriceVnd,
    int? PriorityRadiusKm,
    int? WorkerLimit,
    IReadOnlyList<string> Benefits);
