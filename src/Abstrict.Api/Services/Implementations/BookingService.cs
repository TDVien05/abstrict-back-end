using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Integrations.Storage;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Options;
using Abstrict.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Abstrict.Api.Services.Implementations;

public sealed class BookingService(
    AppDbContext dbContext, BookingWorkflow workflow, TimeProvider timeProvider, IOptions<BookingOptions> options,
    IFileStorage fileStorage) : IBookingService
{
    public const int MaximumPageSize = 50;

    private readonly BookingOptions _options = options.Value;

    private sealed record Draft(
        TimeRange Range, CustomerAddress Address, IReadOnlyList<ChecklistTemplateItem> Items, BookingPrice Price);

    public async Task<BookingQuoteResponse> QuoteAsync(Guid userId, CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var draft = await PrepareAsync(userId, request, now, cancellationToken);
        await EnsureSlotFreeAsync(request.ProviderId, draft.Range, now, cancellationToken);
        var price = draft.Price;
        return new BookingQuoteResponse(
            price.UnitPriceVnd, price.DurationMinutes, price.BaseAmountVnd, price.SurchargeAmountVnd, price.DepositAmountVnd,
            _options.DepositPercent, price.RemainingAmountVnd, _options.HoldMinutes);
    }

    public async Task<BookingDetailResponse> CreateAsync(Guid userId, CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var draft = await PrepareAsync(userId, request, now, cancellationToken);
        Guid bookingId;

        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            // Khóa dòng provider để hai khách đặt cùng lúc được xử lý tuần tự; exclusion constraint là lớp chặn cuối.
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM providers WHERE id = {request.ProviderId} FOR UPDATE", cancellationToken);

            await ReleaseStaleHoldsAsync(request.ProviderId, now, cancellationToken);
            await EnsureSlotFreeAsync(request.ProviderId, draft.Range, now, cancellationToken);

            var activeHolds = await dbContext.Bookings.CountAsync(
                x => x.CustomerUserId == userId && x.Status == BookingStatus.PendingDeposit && x.HoldExpiresAtUtc >= now, cancellationToken);
            if (activeHolds >= _options.MaximumActiveHoldsPerCustomer)
                throw new BookingFlowException(StatusCodes.Status409Conflict, "TOO_MANY_PENDING_BOOKINGS",
                    "Bạn đang có quá nhiều đơn chờ thanh toán cọc. Hãy thanh toán hoặc hủy bớt trước khi đặt thêm.");

            var booking = new Booking
            {
                BookingNumber = await GenerateBookingNumberAsync(now, cancellationToken),
                CustomerUserId = userId,
                ProviderId = request.ProviderId,
                ServiceCategoryId = request.ServiceCategoryId,
                CustomerAddressId = draft.Address.Id,
                Status = BookingStatus.PendingDeposit,
                StartsAtUtc = draft.Range.Start,
                EndsAtUtc = draft.Range.End,
                HoldExpiresAtUtc = now.AddMinutes(_options.HoldMinutes),
                UnitPriceVnd = draft.Price.UnitPriceVnd,
                BaseAmountVnd = draft.Price.BaseAmountVnd,
                SurchargeAmountVnd = draft.Price.SurchargeAmountVnd,
                DepositAmountVnd = draft.Price.DepositAmountVnd,
                AddressSnapshot = BookingMapping.ComposeAddressSnapshot(draft.Address),
                CustomerNote = string.IsNullOrWhiteSpace(request.CustomerNote) ? null : request.CustomerNote.Trim(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            foreach (var item in draft.Items)
            {
                booking.Tasks.Add(new BookingTask
                {
                    TemplateItemId = item.Id,
                    AreaNameSnapshot = item.AreaName,
                    DescriptionSnapshot = item.Description,
                    SortOrder = item.SortOrder,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            }
            booking.StatusHistory.Add(new BookingStatusHistory
            {
                FromStatus = BookingStatus.PendingDeposit,
                ToStatus = BookingStatus.PendingDeposit,
                ChangedByUserId = userId,
                Reason = BookingTimelineReasons.Created,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            dbContext.Bookings.Add(booking);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
            {
                throw SlotUnavailable();
            }

            await transaction.CommitAsync(cancellationToken);
            bookingId = booking.Id;
        }

        dbContext.ChangeTracker.Clear();
        return await GetAsync(userId, bookingId, cancellationToken);
    }

    public async Task<PagedResponse<BookingSummaryResponse>> ListAsync(Guid userId, BookingStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);

        var query = dbContext.Bookings.AsNoTracking().Where(x => x.CustomerUserId == userId);
        if (status is { } value)
            query = query.Where(x => x.Status == value);

        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.StartsAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new BookingSummaryResponse(
                x.Id, x.BookingNumber, x.Status, x.ProviderId, x.Provider.DisplayName, x.ServiceCategory.Code, x.ServiceCategory.Name,
                x.StartsAtUtc, x.EndsAtUtc, x.BaseAmountVnd, x.DepositAmountVnd, x.HoldExpiresAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<BookingSummaryResponse>(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<BookingDetailResponse> GetAsync(Guid userId, Guid bookingId, CancellationToken cancellationToken)
    {
        var booking = await dbContext.Bookings.AsNoTracking().AsSplitQuery()
            .Include(x => x.Provider).Include(x => x.ServiceCategory)
            .Include(x => x.Tasks).Include(x => x.StatusHistory).Include(x => x.Payments).Include(x => x.Photos)
            .SingleOrDefaultAsync(x => x.Id == bookingId && x.CustomerUserId == userId, cancellationToken)
            ?? throw NotFound();
        return BookingMapping.ToDetail(booking, timeProvider.GetUtcNow(), _options);
    }

    public async Task<CancellationPreviewResponse> PreviewCancellationAsync(Guid userId, Guid bookingId, CancellationToken cancellationToken)
    {
        var booking = await dbContext.Bookings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == bookingId && x.CustomerUserId == userId, cancellationToken)
            ?? throw NotFound();
        var paid = (await workflow.FindPaidDepositAsync(booking.Id, cancellationToken))?.AmountVnd ?? 0;
        var outcome = BookingCancellationPolicy.Evaluate(
            booking.Status, booking.StartsAtUtc, timeProvider.GetUtcNow(), booking.BaseAmountVnd, paid, _options);
        DateTimeOffset? freeUntil = booking.Status == BookingStatus.Confirmed
            ? booking.StartsAtUtc.AddMinutes(-_options.FreeCancellationMinutes)
            : null;
        return new CancellationPreviewResponse(outcome.RetainedVnd == 0, outcome.RefundVnd, outcome.RetainedVnd, freeUntil);
    }

    public async Task<BookingDetailResponse> CancelAsync(Guid userId, Guid bookingId, string? reason, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        PendingGatewayRefund? refund = null;

        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var booking = await workflow.LockAsync(bookingId, cancellationToken);
            if (booking is null || booking.CustomerUserId != userId)
                throw NotFound();

            var previousStatus = booking.Status;
            var paid = (await workflow.FindPaidDepositAsync(booking.Id, cancellationToken))?.AmountVnd ?? 0;
            var outcome = BookingCancellationPolicy.Evaluate(previousStatus, booking.StartsAtUtc, now, booking.BaseAmountVnd, paid, _options);

            var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            workflow.Transition(booking, BookingStatus.Cancelled, userId, BookingTimelineReasons.CustomerCancelled, now, trimmedReason);
            booking.CancelledAtUtc = now;
            booking.CancelledByUserId = userId;
            booking.CancellationReason = trimmedReason;

            if (previousStatus == BookingStatus.PendingDeposit)
            {
                await workflow.CancelPendingDepositPaymentsAsync(booking.Id, now, cancellationToken);
            }
            else
            {
                if (outcome.RetainedVnd == 0)
                    refund = await workflow.RefundDepositAsync(booking, now, cancellationToken);
                else
                    await workflow.RetainDepositAsync(booking, outcome, now, cancellationToken);

                if (await workflow.FindProviderUserIdAsync(booking.ProviderId, cancellationToken) is { } providerUserId)
                    workflow.Notify(providerUserId, "booking.cancelled", "Khách đã hủy đơn",
                        $"Đơn {booking.BookingNumber} đã bị khách hủy.", booking, now);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await workflow.SendRefundAsync(refund, cancellationToken);
        dbContext.ChangeTracker.Clear();
        return await GetAsync(userId, bookingId, cancellationToken);
    }

    public async Task<CheckInTokenResponse> IssueCheckInTokenAsync(Guid userId, Guid bookingId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var booking = await workflow.LockAsync(bookingId, cancellationToken);
        if (booking is null || booking.CustomerUserId != userId)
            throw NotFound();
        if (booking.Status != BookingStatus.Confirmed)
            throw new BookingFlowException(StatusCodes.Status409Conflict, "BOOKING_INVALID_STATE",
                "Chỉ đơn đã được xác nhận mới có mã check-in.");
        if (!BookingMapping.IsCheckInOpen(booking, now, _options))
            throw new BookingFlowException(StatusCodes.Status409Conflict, "CHECK_IN_NOT_OPEN",
                $"Mã check-in chỉ mở từ {_options.CheckInOpensMinutesBeforeStart} phút trước giờ bắt đầu đến khi ca kết thúc.");

        var active = await dbContext.CheckInTokens
            .Where(x => x.BookingId == booking.Id && x.RedeemedAtUtc == null && x.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);
        foreach (var previous in active)
        {
            previous.ExpiresAtUtc = now;
            previous.UpdatedAtUtc = now;
        }

        var rawToken = CheckInTokens.Generate();
        var token = new CheckInToken
        {
            BookingId = booking.Id,
            TokenHash = CheckInTokens.Hash(rawToken),
            ExpiresAtUtc = now.AddSeconds(_options.CheckInTokenSeconds),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.CheckInTokens.Add(token);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CheckInTokenResponse(rawToken, token.ExpiresAtUtc, now);
    }

    public async Task<BookingPhotoContent?> OpenPhotoAsync(Guid userId, Guid bookingId, Guid photoId, CancellationToken cancellationToken)
    {
        var key = await dbContext.BookingPhotos.AsNoTracking()
            .Where(x => x.Id == photoId && x.BookingId == bookingId && x.Booking.CustomerUserId == userId)
            .Select(x => x.ObjectKey)
            .SingleOrDefaultAsync(cancellationToken);
        if (key is null)
            return null;

        var stream = await fileStorage.OpenReadAsync(key, cancellationToken);
        return stream is null ? null : new BookingPhotoContent(stream, KycImage.ContentTypeFromKey(key));
    }

    /// <summary>Kiểm tra provider, dịch vụ, địa chỉ, checklist và tính giá. Không kiểm tra slot.</summary>
    private async Task<Draft> PrepareAsync(Guid userId, CreateBookingRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var range = new TimeRange(request.StartsAtUtc.ToUniversalTime(), request.EndsAtUtc.ToUniversalTime());

        var bookable = await dbContext.Providers.AsNoTracking().AnyAsync(
            x => x.Id == request.ProviderId && x.Type == ProviderType.Freelancer
                && x.ApprovalStatus == ApprovalStatus.Approved && x.IsAcceptingBookings, cancellationToken);
        if (!bookable)
            throw new BookingFlowException(StatusCodes.Status409Conflict, "PROVIDER_NOT_BOOKABLE", "Đối tác này hiện không nhận đặt lịch.");

        var hourlyRate = await dbContext.ProviderServices.AsNoTracking()
            .Where(x => x.ProviderId == request.ProviderId && x.ServiceCategoryId == request.ServiceCategoryId
                && x.IsActive && x.ServiceCategory.IsActive)
            .Select(x => (long?)x.HourlyRateVnd)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BookingFlowException(StatusCodes.Status409Conflict, "SERVICE_NOT_OFFERED", "Đối tác không cung cấp dịch vụ này.");

        BookingSlotPolicy.EnsureValidRange(range.Start, range.End, now, _options);

        var address = await dbContext.CustomerAddresses.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.CustomerAddressId && x.CustomerUserId == userId, cancellationToken)
            ?? throw new BookingFlowException(StatusCodes.Status404NotFound, "ADDRESS_NOT_FOUND", "Không tìm thấy địa chỉ.");
        var inServiceArea = await dbContext.ProviderServiceAreas.AsNoTracking().AnyAsync(
            x => x.ProviderId == request.ProviderId && x.ServiceArea.IsActive
                && x.ServiceArea.City == address.City && x.ServiceArea.District == address.District, cancellationToken);
        if (!inServiceArea)
            throw new BookingFlowException(StatusCodes.Status409Conflict, "ADDRESS_OUT_OF_SERVICE_AREA",
                "Địa chỉ nằm ngoài khu vực phục vụ của đối tác.");

        var items = await SelectChecklistItemsAsync(request, cancellationToken);
        var price = BookingPricing.Calculate(hourlyRate, (int)range.Minutes, _options.DepositPercent);
        return new Draft(range, address, items, price);
    }

    private async Task<IReadOnlyList<ChecklistTemplateItem>> SelectChecklistItemsAsync(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var selected = (request.ChecklistItemIds ?? []).ToHashSet();
        var template = await dbContext.ChecklistTemplates.AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.ServiceCategoryId == request.ServiceCategoryId && x.IsActive)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);

        var templateItems = template?.Items ?? [];
        if (selected.Except(templateItems.Select(x => x.Id)).Any())
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "INVALID_CHECKLIST_ITEM",
                "Có mục checklist không thuộc dịch vụ đã chọn.");

        return templateItems.Where(x => x.IsRequired || selected.Contains(x.Id)).OrderBy(x => x.SortOrder).ToList();
    }

    private async Task EnsureSlotFreeAsync(Guid providerId, TimeRange range, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var windows = await dbContext.AvailabilityWindows.AsNoTracking()
            .Where(x => x.ProviderId == providerId && x.IsActive && x.StartsAtUtc <= range.End && x.EndsAtUtc >= range.Start)
            .Select(x => new TimeRange(x.StartsAtUtc, x.EndsAtUtc))
            .ToListAsync(cancellationToken);
        if (!BookingSlotPolicy.IsWithinContinuousFree(windows, range))
            throw new BookingFlowException(StatusCodes.Status409Conflict, "OUTSIDE_AVAILABILITY",
                "Khung giờ đã chọn không nằm trọn trong lịch rảnh của đối tác.");

        var taken = await dbContext.Bookings.AsNoTracking().AnyAsync(
            x => x.ProviderId == providerId
                && BookingStateMachine.SlotHoldingStatuses.Contains(x.Status)
                && (x.Status != BookingStatus.PendingDeposit || x.HoldExpiresAtUtc == null || x.HoldExpiresAtUtc >= now)
                && x.StartsAtUtc < range.End && x.EndsAtUtc > range.Start, cancellationToken);
        if (taken)
            throw SlotUnavailable();
    }

    /// <summary>Giải phóng các hold đã hết hạn của provider để exclusion constraint không bị chặn bởi hold đã chết.</summary>
    private async Task ReleaseStaleHoldsAsync(Guid providerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var stale = await dbContext.Bookings
            .Where(x => x.ProviderId == providerId && x.Status == BookingStatus.PendingDeposit && x.HoldExpiresAtUtc < now)
            .ToListAsync(cancellationToken);
        if (stale.Count == 0)
            return;

        foreach (var booking in stale)
        {
            workflow.Transition(booking, BookingStatus.Expired, null, BookingTimelineReasons.HoldExpired, now);
            await workflow.CancelPendingDepositPaymentsAsync(booking.Id, now, cancellationToken);
            workflow.Notify(booking.CustomerUserId, "booking.expired", "Đơn đã hết hạn giữ chỗ",
                $"Đơn {booking.BookingNumber} đã hết thời gian thanh toán cọc.", booking, now);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> GenerateBookingNumberAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var number = BookingNumbers.Generate(now);
            if (!await dbContext.Bookings.AnyAsync(x => x.BookingNumber == number, cancellationToken))
                return number;
        }
        throw new BookingFlowException(StatusCodes.Status503ServiceUnavailable, "BOOKING_NUMBER_UNAVAILABLE",
            "Không tạo được mã đơn, vui lòng thử lại.");
    }

    private static BookingFlowException NotFound() =>
        new(StatusCodes.Status404NotFound, "BOOKING_NOT_FOUND", "Không tìm thấy đơn đặt lịch.");

    private static BookingFlowException SlotUnavailable() =>
        new(StatusCodes.Status409Conflict, "SLOT_UNAVAILABLE", "Khung giờ này vừa có người đặt. Vui lòng chọn khung giờ khác.");
}
