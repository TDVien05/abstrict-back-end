using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Services.Implementations;

public sealed class FreelancerBookingService(AppDbContext dbContext, BookingWorkflow workflow, TimeProvider timeProvider) : IFreelancerBookingService
{
    public async Task<PagedResponse<FreelancerBookingSummaryResponse>> ListAsync(Guid userId, BookingStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var providerId = await GetProviderIdAsync(userId, cancellationToken);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, BookingService.MaximumPageSize);

        var now = timeProvider.GetUtcNow();
        var query = VisibleBookings(providerId);
        if (status is { } value)
            query = query.Where(x => x.Status == value);

        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.StartsAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new FreelancerBookingSummaryResponse(
                x.Id, x.BookingNumber, x.Status,
                x.CustomerUser.CustomerProfile != null ? x.CustomerUser.CustomerProfile.FullName : string.Empty,
                x.ServiceCategory.Code, x.ServiceCategory.Name, x.StartsAtUtc, x.EndsAtUtc, x.BaseAmountVnd, x.DepositAmountVnd,
                x.CustomerAddress != null ? x.CustomerAddress.District + ", " + x.CustomerAddress.City : null,
                x.ProviderConfirmationDeadlineUtc,
                BookingMapping.FreelancerActions(x.Status, x.ProviderConfirmationDeadlineUtc, now)))
            .ToListAsync(cancellationToken);

        return new PagedResponse<FreelancerBookingSummaryResponse>(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<FreelancerBookingCountsResponse> CountAsync(Guid userId, CancellationToken cancellationToken)
    {
        var providerId = await GetProviderIdAsync(userId, cancellationToken);
        var groups = await VisibleBookings(providerId)
            .GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken);

        var counts = Enum.GetValues<BookingStatus>().Where(x => x != BookingStatus.PendingDeposit).ToDictionary(x => x, _ => 0);
        foreach (var group in groups)
            counts[group.Status] = group.Count;
        return new FreelancerBookingCountsResponse(counts, counts.Values.Sum());
    }

    public async Task<FreelancerBookingDetailResponse> GetAsync(Guid userId, Guid bookingId, CancellationToken cancellationToken)
    {
        var providerId = await GetProviderIdAsync(userId, cancellationToken);
        return await LoadDetailAsync(providerId, bookingId, cancellationToken);
    }

    public async Task<FreelancerBookingDetailResponse> ConfirmAsync(Guid userId, Guid bookingId, CancellationToken cancellationToken)
    {
        var providerId = await GetProviderIdAsync(userId, cancellationToken);
        var now = timeProvider.GetUtcNow();

        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var booking = await LockOwnedAsync(providerId, bookingId, cancellationToken);
            if (booking.Status == BookingStatus.PendingProviderConfirmation && booking.ProviderConfirmationDeadlineUtc < now)
                throw new BookingFlowException(StatusCodes.Status409Conflict, "CONFIRMATION_EXPIRED", "Đã quá hạn xác nhận đơn này.");

            workflow.Transition(booking, BookingStatus.Confirmed, userId, BookingTimelineReasons.ProviderConfirmed, now);
            workflow.Notify(booking.CustomerUserId, "booking.confirmed", "Đơn đã được xác nhận",
                $"Đơn {booking.BookingNumber} đã được người làm xác nhận.", booking, now);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        dbContext.ChangeTracker.Clear();
        return await LoadDetailAsync(providerId, bookingId, cancellationToken);
    }

    public async Task<FreelancerBookingDetailResponse> RejectAsync(Guid userId, Guid bookingId, string? reason, CancellationToken cancellationToken)
    {
        var providerId = await GetProviderIdAsync(userId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        PendingGatewayRefund? refund;

        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var booking = await LockOwnedAsync(providerId, bookingId, cancellationToken);
            if (booking.Status != BookingStatus.PendingProviderConfirmation)
                throw new BookingFlowException(StatusCodes.Status409Conflict, "BOOKING_INVALID_STATE",
                    "Chỉ có thể từ chối đơn đang chờ xác nhận.");

            var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            workflow.Transition(booking, BookingStatus.Rejected, userId, BookingTimelineReasons.ProviderRejected, now, trimmedReason);
            refund = await workflow.RefundDepositAsync(booking, now, cancellationToken);
            workflow.Notify(booking.CustomerUserId, "booking.rejected", "Đơn bị từ chối",
                $"Đơn {booking.BookingNumber} đã bị từ chối, tiền cọc được hoàn đủ.", booking, now);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await workflow.SendRefundAsync(refund, cancellationToken);
        dbContext.ChangeTracker.Clear();
        return await LoadDetailAsync(providerId, bookingId, cancellationToken);
    }

    /// <summary>Đơn chưa từng có cọc (đang giữ chỗ, hoặc hết hạn/hủy trước khi cọc) không hiển thị cho người làm.</summary>
    private IQueryable<Booking> VisibleBookings(Guid providerId) =>
        dbContext.Bookings.AsNoTracking().Where(x => x.ProviderId == providerId
            && x.Status != BookingStatus.PendingDeposit
            && ((x.Status != BookingStatus.Expired && x.Status != BookingStatus.Cancelled)
                || x.Payments.Any(p => p.Purpose == PaymentPurpose.Deposit && p.PaidAtUtc != null)));

    private async Task<Booking> LockOwnedAsync(Guid providerId, Guid bookingId, CancellationToken cancellationToken)
    {
        var booking = await workflow.LockAsync(bookingId, cancellationToken);
        if (booking is null || booking.ProviderId != providerId || booking.Status == BookingStatus.PendingDeposit)
            throw NotFound();
        return booking;
    }

    private async Task<FreelancerBookingDetailResponse> LoadDetailAsync(Guid providerId, Guid bookingId, CancellationToken cancellationToken)
    {
        var booking = await VisibleBookings(providerId).AsSplitQuery()
            .Include(x => x.ServiceCategory).Include(x => x.CustomerAddress)
            .Include(x => x.CustomerUser).ThenInclude(x => x.CustomerProfile)
            .Include(x => x.Tasks).Include(x => x.StatusHistory)
            .SingleOrDefaultAsync(x => x.Id == bookingId, cancellationToken)
            ?? throw NotFound();

        var now = timeProvider.GetUtcNow();
        // Địa chỉ đầy đủ chỉ lộ ra khi đơn đã được xác nhận.
        var addressVisible = booking.Status is BookingStatus.Confirmed or BookingStatus.CheckedIn or BookingStatus.InProgress
            or BookingStatus.PendingAcceptance or BookingStatus.Completed or BookingStatus.Disputed;
        return new FreelancerBookingDetailResponse(
            booking.Id, booking.BookingNumber, booking.Status, booking.CustomerUser.CustomerProfile?.FullName ?? string.Empty,
            booking.ServiceCategory.Code, booking.ServiceCategory.Name, booking.StartsAtUtc, booking.EndsAtUtc,
            booking.BaseAmountVnd, booking.DepositAmountVnd,
            booking.CustomerAddress is null ? null : $"{booking.CustomerAddress.District}, {booking.CustomerAddress.City}",
            booking.ProviderConfirmationDeadlineUtc,
            addressVisible ? booking.AddressSnapshot : null,
            booking.CustomerNote, BookingMapping.Tasks(booking), BookingMapping.Timeline(booking),
            BookingMapping.FreelancerActions(booking.Status, booking.ProviderConfirmationDeadlineUtc, now), now);
    }

    private async Task<Guid> GetProviderIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.FreelancerProfiles.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => (Guid?)x.ProviderId).SingleOrDefaultAsync(cancellationToken)
        ?? throw new BookingFlowException(StatusCodes.Status409Conflict, "PROVIDER_NOT_APPROVED",
            "Tài khoản chưa có hồ sơ người làm.");

    private static BookingFlowException NotFound() =>
        new(StatusCodes.Status404NotFound, "BOOKING_NOT_FOUND", "Không tìm thấy đơn đặt lịch.");
}
