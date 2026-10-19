using Abstrict.Api.Models.Enums;
using Abstrict.Api.Options;

namespace Abstrict.Api.Services.Implementations;

public sealed class BookingFlowException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

public readonly record struct TimeRange(DateTimeOffset Start, DateTimeOffset End)
{
    public bool Overlaps(TimeRange other) => Start < other.End && other.Start < End;
    public bool Contains(TimeRange other) => Start <= other.Start && other.End <= End;
    public double Minutes => (End - Start).TotalMinutes;
}

/// <summary>Giờ hiển thị và ranh giới ngày lịch theo Asia/Ho_Chi_Minh (UTC+7, không DST).</summary>
public static class VietnamTime
{
    public const string TimeZoneId = "Asia/Ho_Chi_Minh";
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static DateOnly Today(DateTimeOffset nowUtc) => DateOnly.FromDateTime(nowUtc.ToOffset(Offset).DateTime);

    public static DateTimeOffset StartOfDayUtc(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), Offset).ToUniversalTime();
}

public sealed record BookingPrice(long UnitPriceVnd, int DurationMinutes, long BaseAmountVnd, long SurchargeAmountVnd, long DepositAmountVnd)
{
    public long RemainingAmountVnd => BaseAmountVnd + SurchargeAmountVnd - DepositAmountVnd;
}

public static class BookingPricing
{
    public static BookingPrice Calculate(long unitPriceVnd, int durationMinutes, int depositPercent)
    {
        var baseAmount = unitPriceVnd * durationMinutes / 60;
        const long surcharge = 0; // TODO: POLICY phụ phí công việc đặc biệt chưa chốt.
        var deposit = (baseAmount * depositPercent + 99) / 100;
        return new BookingPrice(unitPriceVnd, durationMinutes, baseAmount, surcharge, deposit);
    }
}

public static class BookingSlotPolicy
{
    /// <summary>Kiểm tra bước giờ, thời lượng và khoảng đặt trước; ném <see cref="BookingFlowException"/> khi sai.</summary>
    public static void EnsureValidRange(DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc, DateTimeOffset nowUtc, BookingOptions options)
    {
        var stepTicks = options.SlotStepMinutes * TimeSpan.TicksPerMinute;
        if (endsAtUtc <= startsAtUtc || startsAtUtc.UtcTicks % stepTicks != 0 || endsAtUtc.UtcTicks % stepTicks != 0)
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "INVALID_TIME_RANGE",
                $"Giờ bắt đầu và kết thúc phải theo bước {options.SlotStepMinutes} phút và giờ kết thúc phải sau giờ bắt đầu.");

        var minutes = (endsAtUtc - startsAtUtc).TotalMinutes;
        if (minutes < options.MinimumDurationMinutes)
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "DURATION_TOO_SHORT",
                $"Ca làm tối thiểu {options.MinimumDurationMinutes} phút.");
        if (minutes > options.MaximumDurationMinutes)
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "DURATION_TOO_LONG",
                $"Ca làm tối đa {options.MaximumDurationMinutes} phút.");

        if (startsAtUtc < nowUtc.AddMinutes(options.MinimumLeadMinutes))
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "START_TOO_SOON",
                $"Cần đặt trước ít nhất {options.MinimumLeadMinutes} phút.");
        if (startsAtUtc > nowUtc.AddDays(options.MaximumAdvanceDays))
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "START_TOO_FAR",
                $"Chỉ được đặt trước tối đa {options.MaximumAdvanceDays} ngày.");
    }

    /// <summary>Gộp các khoảng chồng hoặc chạm nhau thành các khoảng liên tục, sắp theo thời gian.</summary>
    public static List<TimeRange> Merge(IEnumerable<TimeRange> ranges)
    {
        var merged = new List<TimeRange>();
        foreach (var range in ranges.Where(x => x.End > x.Start).OrderBy(x => x.Start))
        {
            if (merged.Count > 0 && range.Start <= merged[^1].End)
            {
                if (range.End > merged[^1].End)
                    merged[^1] = merged[^1] with { End = range.End };
            }
            else
            {
                merged.Add(range);
            }
        }
        return merged;
    }

    public static bool IsWithinContinuousFree(IEnumerable<TimeRange> availabilityWindows, TimeRange requested) =>
        Merge(availabilityWindows).Any(x => x.Contains(requested));

    /// <summary>Trừ các khoảng bận khỏi các khoảng rảnh, bỏ mảnh ngắn hơn <paramref name="minimumMinutes"/>.</summary>
    public static List<TimeRange> Subtract(IEnumerable<TimeRange> free, IEnumerable<TimeRange> busy, int minimumMinutes)
    {
        var busyMerged = Merge(busy);
        var result = new List<TimeRange>();
        foreach (var window in Merge(free))
        {
            var cursor = window.Start;
            foreach (var block in busyMerged.Where(x => x.Overlaps(window)))
            {
                if (block.Start > cursor)
                    result.Add(new TimeRange(cursor, block.Start));
                if (block.End > cursor)
                    cursor = block.End;
            }
            if (cursor < window.End)
                result.Add(new TimeRange(cursor, window.End));
        }
        return result.Where(x => x.Minutes >= minimumMinutes).ToList();
    }
}

public sealed record CancellationOutcome(long RefundVnd, long ProviderShareVnd, long PlatformShareVnd)
{
    public long RetainedVnd => ProviderShareVnd + PlatformShareVnd;
}

public static class BookingCancellationPolicy
{
    /// <summary>Tính tiền hoàn/giữ khi khách hủy. <paramref name="paidDepositVnd"/> là số cọc đã thu (0 nếu chưa cọc).</summary>
    public static CancellationOutcome Evaluate(
        BookingStatus status, DateTimeOffset startsAtUtc, DateTimeOffset nowUtc, long baseAmountVnd, long paidDepositVnd, BookingOptions options)
    {
        switch (status)
        {
            case BookingStatus.PendingDeposit:
                return new CancellationOutcome(0, 0, 0);
            case BookingStatus.PendingProviderConfirmation:
                // TODO: POLICY hủy khi freelancer chưa xác nhận luôn được hoàn đủ, kể cả sát giờ.
                return new CancellationOutcome(paidDepositVnd, 0, 0);
            case BookingStatus.Confirmed:
                if (startsAtUtc - nowUtc >= TimeSpan.FromMinutes(options.FreeCancellationMinutes))
                    return new CancellationOutcome(paidDepositVnd, 0, 0);
                var providerShare = Math.Min(paidDepositVnd, baseAmountVnd * options.LateCancellationProviderPercent / 100);
                return new CancellationOutcome(0, providerShare, paidDepositVnd - providerShare);
            default:
                throw new BookingFlowException(StatusCodes.Status409Conflict, "BOOKING_NOT_CANCELLABLE",
                    "Đơn ở trạng thái hiện tại không thể hủy.");
        }
    }
}

public static class BookingStateMachine
{
    /// <summary>Các trạng thái đang chiếm slot của người làm.</summary>
    public static readonly BookingStatus[] SlotHoldingStatuses =
    [
        BookingStatus.PendingDeposit, BookingStatus.PendingProviderConfirmation, BookingStatus.Confirmed,
        BookingStatus.CheckedIn, BookingStatus.InProgress, BookingStatus.PendingAcceptance
    ];

    private static readonly Dictionary<BookingStatus, BookingStatus[]> Allowed = new()
    {
        [BookingStatus.PendingDeposit] =
            [BookingStatus.PendingProviderConfirmation, BookingStatus.Confirmed, BookingStatus.Expired, BookingStatus.Cancelled],
        [BookingStatus.PendingProviderConfirmation] =
            [BookingStatus.Confirmed, BookingStatus.Rejected, BookingStatus.Expired, BookingStatus.Cancelled],
        [BookingStatus.Confirmed] = [BookingStatus.Cancelled, BookingStatus.CheckedIn],
        [BookingStatus.CheckedIn] = [BookingStatus.InProgress, BookingStatus.PendingAcceptance],
        [BookingStatus.InProgress] = [BookingStatus.PendingAcceptance],
        [BookingStatus.PendingAcceptance] = [BookingStatus.Completed, BookingStatus.Disputed],
        [BookingStatus.Completed] = [BookingStatus.Disputed]
    };

    public static bool CanTransition(BookingStatus from, BookingStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static void EnsureCanTransition(BookingStatus from, BookingStatus to)
    {
        if (!CanTransition(from, to))
            throw new BookingFlowException(StatusCodes.Status409Conflict, "BOOKING_INVALID_STATE",
                "Đơn không ở trạng thái phù hợp để thực hiện thao tác này.");
    }
}

/// <summary>Mã lý do trong dòng thời gian của đơn; FE dịch sang tiếng Việt theo mã.</summary>
public static class BookingTimelineReasons
{
    public const string Created = "Created";
    public const string DepositPaid = "DepositPaid";
    public const string AutoConfirmed = "AutoConfirmed";
    public const string ProviderConfirmed = "ProviderConfirmed";
    public const string ProviderRejected = "ProviderRejected";
    public const string CheckedIn = "CheckedIn";
    public const string WorkStarted = "WorkStarted";
    public const string WorkFinished = "WorkFinished";
    public const string CustomerAcceptedWork = "CustomerAcceptedWork";
    public const string CustomerDisputeOpened = "CustomerDisputeOpened";
    public const string CustomerCancelled = "CustomerCancelled";
    public const string HoldExpired = "HoldExpired";
    public const string ConfirmationExpired = "ConfirmationExpired";
}

/// <summary>Điều kiện để người làm hoàn tất ca: ca đang diễn ra và đã có ảnh sau khi làm xong.</summary>
public static class BookingCompletionPolicy
{
    public static void EnsureCanComplete(BookingStatus status, int afterPhotoCount)
    {
        if (status is not (BookingStatus.CheckedIn or BookingStatus.InProgress))
            throw new BookingFlowException(StatusCodes.Status409Conflict, "BOOKING_INVALID_STATE",
                "Chỉ hoàn tất ca được khi đang trong ca làm.");
        if (afterPhotoCount < 1)
            throw new BookingFlowException(StatusCodes.Status409Conflict, "AFTER_PHOTO_REQUIRED",
                "Cần ít nhất một ảnh sau khi hoàn tất ca.");
    }
}

/// <summary>Trạng thái checklist mà người làm được phép đặt; <c>ClarificationRequested</c> dành cho luồng khác.</summary>
public static class BookingTaskPolicy
{
    public static readonly BookingTaskStatus[] AllowedStatuses =
        [BookingTaskStatus.Pending, BookingTaskStatus.InProgress, BookingTaskStatus.Completed];

    public static void EnsureValidStatus(BookingTaskStatus status)
    {
        if (!AllowedStatuses.Contains(status))
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "INVALID_TASK_STATUS",
                "Trạng thái mục checklist không hợp lệ.");
    }
}
