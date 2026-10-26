namespace Abstrict.Api.Services.Implementations;

/// <summary>Gom doanh thu gói theo ngày (giờ Việt Nam, UTC+7 cố định) cho báo cáo của admin.</summary>
public static class PromotionReportPolicy
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    /// <summary>Đủ <paramref name="days"/> ngày kết thúc ở hôm nay; ngày không có giao dịch vẫn có dòng 0.</summary>
    public static IReadOnlyList<(DateOnly Date, int Purchases, long RevenueVnd)> Daily(
        IEnumerable<(DateTimeOffset At, long PriceVnd)> paidPurchases, DateTimeOffset now, int days)
    {
        var today = DateOnly.FromDateTime(now.ToOffset(VietnamOffset).DateTime);
        var buckets = Enumerable.Range(0, days).Select(i => today.AddDays(i - (days - 1))).ToDictionary(d => d, _ => (Purchases: 0, Revenue: 0L));
        foreach (var (at, price) in paidPurchases)
        {
            var day = DateOnly.FromDateTime(at.ToOffset(VietnamOffset).DateTime);
            if (buckets.TryGetValue(day, out var current))
                buckets[day] = (current.Purchases + 1, current.Revenue + price);
        }

        return buckets.OrderBy(x => x.Key).Select(x => (x.Key, x.Value.Purchases, x.Value.Revenue)).ToList();
    }
}
