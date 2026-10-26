using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Services.Implementations;

/// <summary>Quy tắc mua gói tăng hiển thị: ai được mua, thời hạn hiệu lực và trạng thái hiển thị.</summary>
public static class PromotionPurchasePolicy
{
    /// <summary>Chỉ đối tác đã được duyệt mới mua gói (hồ sơ chưa duyệt chưa hiển thị công khai nên chưa cần tăng hiển thị).</summary>
    public static void EnsureCanPurchase(ApprovalStatus approval)
    {
        if (approval != ApprovalStatus.Approved)
            throw new BookingFlowException(StatusCodes.Status409Conflict, "PROVIDER_NOT_APPROVED", "Chỉ hồ sơ đã được duyệt mới mua được gói hiển thị.");
    }

    /// <summary>Gói chỉ mua được khi đang bán và đúng đối tượng của đối tác.</summary>
    public static void EnsurePlanAvailable(bool isActive, PromotionAudience planAudience, ProviderType providerType)
    {
        var audience = providerType == ProviderType.Company ? PromotionAudience.Company : PromotionAudience.Freelancer;
        if (!isActive || planAudience != audience)
            throw new BookingFlowException(StatusCodes.Status409Conflict, "PLAN_NOT_AVAILABLE", "Gói này hiện không bán cho bạn.");
    }

    /// <summary>Gói mới bắt đầu ngay, hoặc nối tiếp ngay sau gói đang/sắp hiệu lực để không mất ngày đã mua.</summary>
    public static (DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc) ComputeWindow(DateTimeOffset now, DateTimeOffset? latestActiveEndUtc, int durationDays)
    {
        var start = latestActiveEndUtc is { } end && end > now ? end : now;
        return (start, start.AddDays(durationDays));
    }

    /// <summary>Trạng thái hiển thị cho đối tác: lượt đã quá hạn được coi là Expired dù DB chưa cập nhật.</summary>
    public static PromotionPurchaseStatus EffectiveStatus(PromotionPurchaseStatus status, DateTimeOffset? endsAtUtc, DateTimeOffset now) =>
        status == PromotionPurchaseStatus.Active && endsAtUtc <= now ? PromotionPurchaseStatus.Expired : status;

    /// <summary>Lượt đang có hiệu lực ngay lúc này (khác lượt đã mua nhưng chờ nối tiếp).</summary>
    public static bool IsCurrent(PromotionPurchaseStatus status, DateTimeOffset? startsAtUtc, DateTimeOffset? endsAtUtc, DateTimeOffset now) =>
        status == PromotionPurchaseStatus.Active && startsAtUtc <= now && endsAtUtc > now;
}
