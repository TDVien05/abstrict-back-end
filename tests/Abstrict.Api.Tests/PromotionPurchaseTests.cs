using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Implementations;

namespace Abstrict.Api.Tests;

public sealed class PromotionReportPolicyTests
{
    // 2099-01-10 18:00 UTC = 01:00 ngày 11/1 giờ Việt Nam.
    private static readonly DateTimeOffset Now = new(2099, 1, 10, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Daily_ReturnsEverySlotEndingToday_InVietnamTime()
    {
        var rows = PromotionReportPolicy.Daily([], Now, 7);
        Assert.Equal(7, rows.Count);
        Assert.Equal(new DateOnly(2099, 1, 5), rows[0].Date);
        Assert.Equal(new DateOnly(2099, 1, 11), rows[^1].Date);
        Assert.All(rows, row => Assert.Equal((0, 0L), (row.Purchases, row.RevenueVnd)));
    }

    [Fact]
    public void Daily_BucketsByVietnamDayAndIgnoresOlderPurchases()
    {
        var rows = PromotionReportPolicy.Daily(
        [
            (Now.AddHours(-0.5), 50_000),   // 17:30 UTC ngày 10 = 00:30 ngày 11 VN
            (Now.AddHours(-2), 20_000),     // 16:00 UTC ngày 10 = 23:00 ngày 10 VN
            (Now.AddHours(-3), 30_000),
            (Now.AddDays(-30), 999_000),
        ], Now, 7);
        Assert.Equal((1, 50_000L), (rows[^1].Purchases, rows[^1].RevenueVnd));
        Assert.Equal((2, 50_000L), (rows[^2].Purchases, rows[^2].RevenueVnd));
        Assert.Equal(100_000L, rows.Sum(x => x.RevenueVnd));
    }
}

public sealed class PromotionPurchasePolicyTests
{
    private static readonly DateTimeOffset Now = new(2099, 1, 10, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Window_StartsNowWhenNothingIsActive()
    {
        var (start, end) = PromotionPurchasePolicy.ComputeWindow(Now, null, 7);
        Assert.Equal(Now, start);
        Assert.Equal(Now.AddDays(7), end);
    }

    [Fact]
    public void Window_ChainsAfterTheLatestActivePurchase()
    {
        var (start, end) = PromotionPurchasePolicy.ComputeWindow(Now, Now.AddDays(3), 30);
        Assert.Equal(Now.AddDays(3), start);
        Assert.Equal(Now.AddDays(33), end);
    }

    [Fact]
    public void Window_IgnoresAnAlreadyEndedPurchase() =>
        Assert.Equal(Now, PromotionPurchasePolicy.ComputeWindow(Now, Now.AddDays(-1), 7).StartsAtUtc);

    [Theory]
    [InlineData(ApprovalStatus.Draft)]
    [InlineData(ApprovalStatus.Submitted)]
    [InlineData(ApprovalStatus.Rejected)]
    [InlineData(ApprovalStatus.Suspended)]
    public void OnlyApprovedProvidersCanPurchase(ApprovalStatus status) =>
        Assert.Equal("PROVIDER_NOT_APPROVED", Assert.Throws<BookingFlowException>(() => PromotionPurchasePolicy.EnsureCanPurchase(status)).Code);

    [Fact]
    public void ApprovedProviderCanPurchase() => PromotionPurchasePolicy.EnsureCanPurchase(ApprovalStatus.Approved);

    [Theory]
    [InlineData(false, PromotionAudience.Freelancer, ProviderType.Freelancer)]
    [InlineData(true, PromotionAudience.Company, ProviderType.Freelancer)]
    [InlineData(true, PromotionAudience.Freelancer, ProviderType.Company)]
    public void PlanMustBeActiveAndMatchTheProviderType(bool active, PromotionAudience audience, ProviderType type) =>
        Assert.Equal("PLAN_NOT_AVAILABLE", Assert.Throws<BookingFlowException>(() => PromotionPurchasePolicy.EnsurePlanAvailable(active, audience, type)).Code);

    [Fact]
    public void MatchingActivePlanIsAvailable()
    {
        PromotionPurchasePolicy.EnsurePlanAvailable(true, PromotionAudience.Freelancer, ProviderType.Freelancer);
        PromotionPurchasePolicy.EnsurePlanAvailable(true, PromotionAudience.Company, ProviderType.Company);
    }

    [Fact]
    public void ActivePurchasePastItsEndIsReportedAsExpired()
    {
        Assert.Equal(PromotionPurchaseStatus.Expired, PromotionPurchasePolicy.EffectiveStatus(PromotionPurchaseStatus.Active, Now.AddMinutes(-1), Now));
        Assert.Equal(PromotionPurchaseStatus.Active, PromotionPurchasePolicy.EffectiveStatus(PromotionPurchaseStatus.Active, Now.AddMinutes(1), Now));
        Assert.Equal(PromotionPurchaseStatus.PaymentFailed, PromotionPurchasePolicy.EffectiveStatus(PromotionPurchaseStatus.PaymentFailed, null, Now));
    }

    [Fact]
    public void QueuedPurchaseIsNotCurrentUntilItStarts()
    {
        Assert.True(PromotionPurchasePolicy.IsCurrent(PromotionPurchaseStatus.Active, Now.AddDays(-1), Now.AddDays(1), Now));
        Assert.False(PromotionPurchasePolicy.IsCurrent(PromotionPurchaseStatus.Active, Now.AddDays(1), Now.AddDays(8), Now));
        Assert.False(PromotionPurchasePolicy.IsCurrent(PromotionPurchaseStatus.PendingPayment, null, null, Now));
    }
}
