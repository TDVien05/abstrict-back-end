using Abstrict.Api.Common;
using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Integrations.Storage;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Options;
using Abstrict.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Abstrict.Api.Services.Implementations;

public sealed class FreelancerBookingService(
    AppDbContext dbContext, BookingWorkflow workflow, TimeProvider timeProvider, IFileStorage fileStorage,
    IOptions<BookingOptions> options) : IFreelancerBookingService
{
    private readonly BookingOptions _options = options.Value;

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
                BookingMapping.FreelancerActions(x.Status, x.ProviderConfirmationDeadlineUtc, x.StartsAtUtc, x.EndsAtUtc, now, _options)))
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

    public async Task<FreelancerBookingDetailResponse> CheckInAsync(Guid userId, Guid bookingId, string token, CancellationToken cancellationToken)
    {
        var providerId = await GetProviderIdAsync(userId, cancellationToken);
        var now = timeProvider.GetUtcNow();

        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var booking = await LockOwnedAsync(providerId, bookingId, cancellationToken);
            if (booking.Status != BookingStatus.Confirmed)
                throw new BookingFlowException(StatusCodes.Status409Conflict, "BOOKING_INVALID_STATE",
                    "Chỉ đơn đã được xác nhận mới check-in được.");
            if (!BookingMapping.IsCheckInOpen(booking, now, _options))
                throw new BookingFlowException(StatusCodes.Status409Conflict, "CHECK_IN_NOT_OPEN",
                    $"Mã check-in chỉ mở từ {_options.CheckInOpensMinutesBeforeStart} phút trước giờ bắt đầu đến khi ca kết thúc.");

            var hash = CheckInTokens.Hash(token);
            var checkInToken = await dbContext.CheckInTokens.SingleOrDefaultAsync(
                x => x.BookingId == booking.Id && x.TokenHash == hash && x.RedeemedAtUtc == null, cancellationToken)
                ?? throw new BookingFlowException(StatusCodes.Status409Conflict, "CHECK_IN_TOKEN_INVALID", "Mã check-in không hợp lệ.");
            if (checkInToken.ExpiresAtUtc <= now)
                throw new BookingFlowException(StatusCodes.Status409Conflict, "CHECK_IN_TOKEN_EXPIRED", "Mã check-in đã hết hạn.");

            checkInToken.RedeemedAtUtc = now;
            checkInToken.RedeemedByUserId = userId;
            checkInToken.UpdatedAtUtc = now;
            booking.CheckedInAtUtc = now;
            workflow.Transition(booking, BookingStatus.CheckedIn, userId, BookingTimelineReasons.CheckedIn, now);
            workflow.Notify(booking.CustomerUserId, "booking.checked_in", "Người làm đã check-in",
                $"Đơn {booking.BookingNumber} đã được check-in, ca làm bắt đầu.", booking, now);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        dbContext.ChangeTracker.Clear();
        return await LoadDetailAsync(providerId, bookingId, cancellationToken);
    }

    public async Task<FreelancerBookingDetailResponse> UpdateTaskStatusAsync(
        Guid userId, Guid bookingId, Guid taskId, BookingTaskStatus status, CancellationToken cancellationToken)
    {
        BookingTaskPolicy.EnsureValidStatus(status);

        var providerId = await GetProviderIdAsync(userId, cancellationToken);
        var now = timeProvider.GetUtcNow();

        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var booking = await LockOwnedAsync(providerId, bookingId, cancellationToken);
            if (booking.Status is not (BookingStatus.CheckedIn or BookingStatus.InProgress))
                throw new BookingFlowException(StatusCodes.Status409Conflict, "BOOKING_INVALID_STATE",
                    "Chỉ cập nhật tiến độ được khi đang trong ca làm.");

            var task = await dbContext.BookingTasks.SingleOrDefaultAsync(
                x => x.Id == taskId && x.BookingId == booking.Id, cancellationToken)
                ?? throw new BookingFlowException(StatusCodes.Status404NotFound, "BOOKING_TASK_NOT_FOUND",
                    "Không tìm thấy mục checklist trong đơn này.");

            task.Status = status;
            task.CompletedAtUtc = status == BookingTaskStatus.Completed ? now : null;
            task.UpdatedAtUtc = now;

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        dbContext.ChangeTracker.Clear();
        return await LoadDetailAsync(providerId, bookingId, cancellationToken);
    }

    public async Task<FreelancerBookingDetailResponse> CompleteWorkAsync(Guid userId, Guid bookingId, CancellationToken cancellationToken)
    {
        var providerId = await GetProviderIdAsync(userId, cancellationToken);
        var now = timeProvider.GetUtcNow();

        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var booking = await LockOwnedAsync(providerId, bookingId, cancellationToken);
            var afterPhotoCount = await dbContext.BookingPhotos.CountAsync(
                x => x.BookingId == booking.Id && x.Type == BookingPhotoType.After, cancellationToken);
            BookingCompletionPolicy.EnsureCanComplete(booking.Status, afterPhotoCount);

            workflow.Transition(booking, BookingStatus.PendingAcceptance, userId, BookingTimelineReasons.WorkFinished, now);
            workflow.Notify(booking.CustomerUserId, "booking.work_finished", "Người làm đã hoàn tất ca",
                $"Đơn {booking.BookingNumber} đã hoàn tất công việc, chờ bạn nghiệm thu.", booking, now);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        dbContext.ChangeTracker.Clear();
        return await LoadDetailAsync(providerId, bookingId, cancellationToken);
    }

    public async Task<FreelancerBookingDetailResponse> UploadPhotoAsync(
        Guid userId, Guid bookingId, BookingPhotoType type, Guid? bookingTaskId, string? caption, long length, Stream content, CancellationToken cancellationToken)
    {
        if (type is not (BookingPhotoType.Before or BookingPhotoType.After))
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "INVALID_PHOTO_TYPE",
                "Chỉ nhận ảnh trước (Before) hoặc sau (After) khi làm việc.");
        if (length <= 0)
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "EMPTY_FILE", "Tệp tải lên đang trống.");
        if (length > KycImage.MaximumBytes)
            throw new BookingFlowException(StatusCodes.Status413PayloadTooLarge, "FILE_TOO_LARGE", "Ảnh không được vượt quá 5 MB.");

        // Tin nội dung thực của file, không tin Content-Type hay đuôi file do client gửi.
        var header = new byte[8];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        var image = KycImage.Detect(header.AsSpan(0, read))
            ?? throw new BookingFlowException(StatusCodes.Status415UnsupportedMediaType, "UNSUPPORTED_IMAGE", "Chỉ chấp nhận ảnh JPG hoặc PNG.");
        content.Position = 0;

        var providerId = await GetProviderIdAsync(userId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        string newKey;

        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var booking = await LockOwnedAsync(providerId, bookingId, cancellationToken);
            if (booking.Status is not (BookingStatus.CheckedIn or BookingStatus.InProgress))
                throw new BookingFlowException(StatusCodes.Status409Conflict, "BOOKING_INVALID_STATE",
                    "Chỉ tải ảnh tiến độ được khi ca đã check-in.");

            if (bookingTaskId is { } taskId && !await dbContext.BookingTasks.AnyAsync(
                    x => x.Id == taskId && x.BookingId == booking.Id, cancellationToken))
                throw new BookingFlowException(StatusCodes.Status400BadRequest, "INVALID_BOOKING_TASK", "Mục checklist không thuộc đơn này.");

            newKey = await fileStorage.SaveAsync($"bookings/{booking.Id:N}", image.Extension, content, cancellationToken);
            dbContext.BookingPhotos.Add(new BookingPhoto
            {
                BookingId = booking.Id,
                BookingTaskId = bookingTaskId,
                UploadedByUserId = userId,
                Type = type,
                ObjectKey = newKey,
                Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(),
                TakenAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            // Ảnh Before đầu tiên đánh dấu ca đã bắt đầu.
            if (booking.Status == BookingStatus.CheckedIn && type == BookingPhotoType.Before)
            {
                workflow.Transition(booking, BookingStatus.InProgress, userId, BookingTimelineReasons.WorkStarted, now);
                workflow.Notify(booking.CustomerUserId, "booking.started", "Ca làm đã bắt đầu",
                    $"Đơn {booking.BookingNumber} đã bắt đầu, người làm đang cập nhật ảnh tiến độ.", booking, now);
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                await fileStorage.DeleteAsync(newKey, CancellationToken.None);
                throw;
            }

            await transaction.CommitAsync(cancellationToken);
        }

        dbContext.ChangeTracker.Clear();
        return await LoadDetailAsync(providerId, bookingId, cancellationToken);
    }

    public async Task<BookingPhotoContent?> OpenPhotoAsync(Guid userId, Guid bookingId, Guid photoId, CancellationToken cancellationToken)
    {
        var providerId = await GetProviderIdAsync(userId, cancellationToken);
        var key = await dbContext.BookingPhotos.AsNoTracking()
            .Where(x => x.Id == photoId && x.BookingId == bookingId && x.Booking.ProviderId == providerId)
            .Select(x => x.ObjectKey)
            .SingleOrDefaultAsync(cancellationToken);
        if (key is null)
            return null;

        var stream = await fileStorage.OpenReadAsync(key, cancellationToken);
        return stream is null ? null : new BookingPhotoContent(stream, KycImage.ContentTypeFromKey(key));
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
            .Include(x => x.Tasks).Include(x => x.StatusHistory).Include(x => x.Photos)
            .SingleOrDefaultAsync(x => x.Id == bookingId, cancellationToken)
            ?? throw NotFound();

        var now = timeProvider.GetUtcNow();
        // Địa chỉ đầy đủ chỉ lộ ra khi đơn đã được xác nhận.
        var addressVisible = booking.Status is BookingStatus.Confirmed or BookingStatus.CheckedIn or BookingStatus.InProgress
            or BookingStatus.PendingAcceptance or BookingStatus.Completed or BookingStatus.Disputed;
        var photosRoute = $"/{ApiRoutes.Version1}/freelancer/bookings/{booking.Id}/photos";
        return new FreelancerBookingDetailResponse(
            booking.Id, booking.BookingNumber, booking.Status, booking.CustomerUser.CustomerProfile?.FullName ?? string.Empty,
            booking.ServiceCategory.Code, booking.ServiceCategory.Name, booking.StartsAtUtc, booking.EndsAtUtc,
            booking.BaseAmountVnd, booking.DepositAmountVnd,
            booking.CustomerAddress is null ? null : $"{booking.CustomerAddress.District}, {booking.CustomerAddress.City}",
            booking.ProviderConfirmationDeadlineUtc,
            addressVisible ? booking.AddressSnapshot : null,
            booking.CustomerNote, BookingMapping.Tasks(booking), BookingMapping.Timeline(booking),
            BookingMapping.FreelancerActions(booking.Status, booking.ProviderConfirmationDeadlineUtc, booking.StartsAtUtc, booking.EndsAtUtc, now, _options),
            now, BookingMapping.Photos(booking, photosRoute));
    }

    private async Task<Guid> GetProviderIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.FreelancerProfiles.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => (Guid?)x.ProviderId).SingleOrDefaultAsync(cancellationToken)
        ?? throw new BookingFlowException(StatusCodes.Status409Conflict, "PROVIDER_NOT_APPROVED",
            "Tài khoản chưa có hồ sơ người làm.");

    private static BookingFlowException NotFound() =>
        new(StatusCodes.Status404NotFound, "BOOKING_NOT_FOUND", "Không tìm thấy đơn đặt lịch.");
}
