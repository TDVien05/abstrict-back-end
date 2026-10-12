using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Options;
using Abstrict.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Abstrict.Api.Services.Implementations;

public sealed class AvailabilityService(AppDbContext dbContext, TimeProvider timeProvider, IOptions<BookingOptions> options) : IAvailabilityService
{
    public const int MaximumPublicDays = 14;
    public const int MaximumOwnDays = 60;

    private readonly BookingOptions _options = options.Value;

    public async Task<IReadOnlyList<AvailabilityWindowResponse>> ListMineAsync(Guid userId, DateOnly? from, int days, CancellationToken cancellationToken)
    {
        var providerId = await GetApprovedProviderIdAsync(userId, cancellationToken);
        var (rangeStart, rangeEnd) = Range(from, Math.Clamp(days, 1, MaximumOwnDays));

        return await dbContext.AvailabilityWindows.AsNoTracking()
            .Where(x => x.ProviderId == providerId && x.IsActive && x.StartsAtUtc < rangeEnd && x.EndsAtUtc > rangeStart)
            .OrderBy(x => x.StartsAtUtc)
            .Select(x => new AvailabilityWindowResponse(x.Id, x.StartsAtUtc, x.EndsAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<AvailabilityWindowResponse> CreateAsync(Guid userId, CreateAvailabilityWindowRequest request, CancellationToken cancellationToken)
    {
        var providerId = await GetApprovedProviderIdAsync(userId, cancellationToken);
        var start = request.StartsAtUtc.ToUniversalTime();
        var end = request.EndsAtUtc.ToUniversalTime();
        var now = timeProvider.GetUtcNow();

        var stepTicks = _options.SlotStepMinutes * TimeSpan.TicksPerMinute;
        if (end <= start || start.UtcTicks % stepTicks != 0 || end.UtcTicks % stepTicks != 0)
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "INVALID_TIME_RANGE",
                $"Giờ bắt đầu và kết thúc phải theo bước {_options.SlotStepMinutes} phút và giờ kết thúc phải sau giờ bắt đầu.");
        if ((end - start).TotalMinutes < _options.MinimumDurationMinutes)
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "DURATION_TOO_SHORT",
                $"Khoảng rảnh tối thiểu {_options.MinimumDurationMinutes} phút.");
        if (start <= now)
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "START_TOO_SOON", "Khoảng rảnh phải bắt đầu ở tương lai.");

        var overlaps = await dbContext.AvailabilityWindows.AnyAsync(
            x => x.ProviderId == providerId && x.IsActive && x.StartsAtUtc < end && x.EndsAtUtc > start, cancellationToken);
        if (overlaps)
            throw new BookingFlowException(StatusCodes.Status409Conflict, "AVAILABILITY_OVERLAP", "Khoảng rảnh bị chồng với một khoảng rảnh khác.");

        var window = new AvailabilityWindow
        {
            ProviderId = providerId,
            StartsAtUtc = start,
            EndsAtUtc = end,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.AvailabilityWindows.Add(window);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AvailabilityWindowResponse(window.Id, window.StartsAtUtc, window.EndsAtUtc);
    }

    public async Task DeleteAsync(Guid userId, Guid windowId, CancellationToken cancellationToken)
    {
        var providerId = await GetApprovedProviderIdAsync(userId, cancellationToken);
        var window = await dbContext.AvailabilityWindows.SingleOrDefaultAsync(x => x.Id == windowId && x.ProviderId == providerId, cancellationToken)
            ?? throw new BookingFlowException(StatusCodes.Status404NotFound, "AVAILABILITY_NOT_FOUND", "Không tìm thấy khoảng rảnh.");

        var now = timeProvider.GetUtcNow();
        var inUse = await SlotHoldingBookings(providerId, now)
            .AnyAsync(x => x.StartsAtUtc < window.EndsAtUtc && x.EndsAtUtc > window.StartsAtUtc, cancellationToken);
        if (inUse)
            throw new BookingFlowException(StatusCodes.Status409Conflict, "AVAILABILITY_IN_USE", "Khoảng rảnh đang có đơn đặt, không thể xóa.");

        dbContext.AvailabilityWindows.Remove(window);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProviderAvailabilityResponse?> GetPublicAsync(Guid providerId, DateOnly? from, int days, CancellationToken cancellationToken)
    {
        var bookable = await dbContext.Providers.AsNoTracking().AnyAsync(
            x => x.Id == providerId && x.Type == ProviderType.Freelancer && x.ApprovalStatus == ApprovalStatus.Approved, cancellationToken);
        if (!bookable)
            return null;

        days = Math.Clamp(days, 1, MaximumPublicDays);
        var now = timeProvider.GetUtcNow();
        var firstDay = from ?? VietnamTime.Today(now);
        var (rangeStart, rangeEnd) = Range(firstDay, days);

        var windows = await dbContext.AvailabilityWindows.AsNoTracking()
            .Where(x => x.ProviderId == providerId && x.IsActive && x.StartsAtUtc < rangeEnd && x.EndsAtUtc > rangeStart)
            .Select(x => new TimeRange(x.StartsAtUtc, x.EndsAtUtc))
            .ToListAsync(cancellationToken);
        var busy = await SlotHoldingBookings(providerId, now)
            .Where(x => x.StartsAtUtc < rangeEnd && x.EndsAtUtc > rangeStart)
            .Select(x => new TimeRange(x.StartsAtUtc, x.EndsAtUtc))
            .ToListAsync(cancellationToken);

        // Phần đã qua hoặc quá sát giờ được coi như không còn đặt được.
        var stepTicks = _options.SlotStepMinutes * TimeSpan.TicksPerMinute;
        var leadTicks = now.AddMinutes(_options.MinimumLeadMinutes).UtcTicks;
        var earliestStart = new DateTimeOffset((leadTicks + stepTicks - 1) / stepTicks * stepTicks, TimeSpan.Zero);
        var blocked = busy.Append(new TimeRange(DateTimeOffset.MinValue, earliestStart));
        var free = BookingSlotPolicy.Subtract(windows, blocked, _options.MinimumDurationMinutes);
        var busyMerged = BookingSlotPolicy.Merge(busy);

        var result = new List<AvailabilityDayResponse>(days);
        for (var i = 0; i < days; i++)
        {
            var date = firstDay.AddDays(i);
            var day = new TimeRange(VietnamTime.StartOfDayUtc(date), VietnamTime.StartOfDayUtc(date.AddDays(1)));
            result.Add(new AvailabilityDayResponse(
                date,
                Clip(free, day).Where(x => x.Minutes >= _options.MinimumDurationMinutes).Select(ToResponse).ToList(),
                Clip(busyMerged, day).Select(ToResponse).ToList()));
        }

        return new ProviderAvailabilityResponse(
            providerId, VietnamTime.TimeZoneId, _options.SlotStepMinutes, _options.MinimumDurationMinutes, result);
    }

    /// <summary>Booking đang chiếm slot của provider; hold đã hết hạn không còn chiếm slot.</summary>
    private IQueryable<Booking> SlotHoldingBookings(Guid providerId, DateTimeOffset now) =>
        dbContext.Bookings.AsNoTracking().Where(x => x.ProviderId == providerId
            && BookingStateMachine.SlotHoldingStatuses.Contains(x.Status)
            && (x.Status != BookingStatus.PendingDeposit || x.HoldExpiresAtUtc == null || x.HoldExpiresAtUtc >= now));

    private (DateTimeOffset Start, DateTimeOffset End) Range(DateOnly? from, int days)
    {
        var firstDay = from ?? VietnamTime.Today(timeProvider.GetUtcNow());
        return (VietnamTime.StartOfDayUtc(firstDay), VietnamTime.StartOfDayUtc(firstDay.AddDays(days)));
    }

    private static IEnumerable<TimeRange> Clip(IEnumerable<TimeRange> ranges, TimeRange day) =>
        ranges.Where(x => x.Overlaps(day))
            .Select(x => new TimeRange(x.Start > day.Start ? x.Start : day.Start, x.End < day.End ? x.End : day.End));

    private static TimeRangeResponse ToResponse(TimeRange range) => new(range.Start, range.End);

    private async Task<Guid> GetApprovedProviderIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.FreelancerProfiles.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.ProviderId, x.Provider.ApprovalStatus })
            .SingleOrDefaultAsync(cancellationToken);
        if (profile is null || profile.ApprovalStatus != ApprovalStatus.Approved)
            throw new BookingFlowException(StatusCodes.Status409Conflict, "PROVIDER_NOT_APPROVED",
                "Hồ sơ chưa được duyệt nên chưa thể thực hiện thao tác này.");
        return profile.ProviderId;
    }
}
