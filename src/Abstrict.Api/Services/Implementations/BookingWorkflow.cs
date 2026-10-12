using Abstrict.Api.Data;
using Abstrict.Api.Integrations.Payments;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Services.Implementations;

/// <summary>Khoản hoàn cần gửi tới cổng thanh toán sau khi transaction DB đã commit.</summary>
public sealed record PendingGatewayRefund(string ProviderTransactionId, long AmountVnd);

/// <summary>
/// Phần dùng chung của các service booking: khóa dòng, đổi trạng thái kèm lịch sử, ghi sổ tiền cọc và thông báo.
/// Mọi hàm ở đây chỉ sửa DbContext; nơi gọi chịu trách nhiệm transaction và SaveChanges.
/// </summary>
public sealed class BookingWorkflow(AppDbContext dbContext, IPaymentGateway paymentGateway, ILogger<BookingWorkflow> logger)
{
    /// <summary>Khóa dòng booking tới hết transaction hiện tại; phải gọi bên trong transaction.</summary>
    public async Task<Booking?> LockAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Bookings
            .FromSqlInterpolated($"SELECT * FROM bookings WHERE id = {bookingId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }

    public void Transition(Booking booking, BookingStatus to, Guid? changedByUserId, string reason, DateTimeOffset now, string? note = null)
    {
        BookingStateMachine.EnsureCanTransition(booking.Status, to);
        dbContext.BookingStatusHistory.Add(new BookingStatusHistory
        {
            BookingId = booking.Id,
            FromStatus = booking.Status,
            ToStatus = to,
            ChangedByUserId = changedByUserId,
            Reason = reason,
            Note = note,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        booking.Status = to;
        booking.UpdatedAtUtc = now;
    }

    public async Task<Payment?> FindPaidDepositAsync(Guid bookingId, CancellationToken cancellationToken) =>
        await dbContext.Payments.SingleOrDefaultAsync(
            x => x.BookingId == bookingId && x.Purpose == PaymentPurpose.Deposit && x.Status == PaymentStatus.Succeeded, cancellationToken);

    public async Task CancelPendingDepositPaymentsAsync(Guid bookingId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var pending = await dbContext.Payments
            .Where(x => x.BookingId == bookingId && x.Purpose == PaymentPurpose.Deposit
                && (x.Status == PaymentStatus.Pending || x.Status == PaymentStatus.Processing))
            .ToListAsync(cancellationToken);
        foreach (var payment in pending)
        {
            payment.Status = PaymentStatus.Cancelled;
            payment.UpdatedAtUtc = now;
        }
    }

    public void RecordDepositHeld(Booking booking, Payment payment, DateTimeOffset now) =>
        dbContext.MoneyMovements.Add(new MoneyMovement
        {
            BookingId = booking.Id,
            PaymentId = payment.Id,
            Type = MoneyMovementType.DepositHeld,
            Status = MoneyMovementStatus.Held,
            AmountVnd = payment.AmountVnd,
            Reference = DepositHeldReference(booking.Id),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

    /// <summary>Hoàn đủ cọc đã thu (nếu có) và trả về khoản cần gửi cổng thanh toán sau khi commit.</summary>
    public async Task<PendingGatewayRefund?> RefundDepositAsync(Booking booking, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var payment = await FindPaidDepositAsync(booking.Id, cancellationToken);
        return payment is null ? null : await RefundPaymentAsync(booking, payment, now, cancellationToken);
    }

    public async Task<PendingGatewayRefund?> RefundPaymentAsync(Booking booking, Payment payment, DateTimeOffset now, CancellationToken cancellationToken)
    {
        payment.Status = PaymentStatus.Refunded;
        payment.RefundedAtUtc = now;
        payment.UpdatedAtUtc = now;
        await CloseDepositHeldAsync(booking.Id, MoneyMovementStatus.Reversed, now, cancellationToken);
        dbContext.MoneyMovements.Add(new MoneyMovement
        {
            BookingId = booking.Id,
            PaymentId = payment.Id,
            Type = MoneyMovementType.CustomerRefund,
            Status = MoneyMovementStatus.Posted,
            AmountVnd = payment.AmountVnd,
            Reference = $"booking:{booking.Id}:refund",
            PostedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        return payment.ProviderTransactionId is null ? null : new PendingGatewayRefund(payment.ProviderTransactionId, payment.AmountVnd);
    }

    /// <summary>Hủy muộn: cọc không hoàn, chia cho bên cung cấp và nền tảng.</summary>
    public async Task RetainDepositAsync(Booking booking, CancellationOutcome outcome, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var payment = await FindPaidDepositAsync(booking.Id, cancellationToken);
        if (payment is null)
            return;

        await CloseDepositHeldAsync(booking.Id, MoneyMovementStatus.Posted, now, cancellationToken);
        if (outcome.ProviderShareVnd > 0)
        {
            dbContext.MoneyMovements.Add(new MoneyMovement
            {
                BookingId = booking.Id,
                PaymentId = payment.Id,
                BeneficiaryProviderId = booking.ProviderId,
                Type = MoneyMovementType.ProviderEarning,
                Status = MoneyMovementStatus.Pending, // TODO: POLICY luồng chi trả cho bên cung cấp chưa chốt.
                AmountVnd = outcome.ProviderShareVnd,
                Reference = $"booking:{booking.Id}:cancel-provider",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }
        if (outcome.PlatformShareVnd > 0)
        {
            dbContext.MoneyMovements.Add(new MoneyMovement
            {
                BookingId = booking.Id,
                PaymentId = payment.Id,
                Type = MoneyMovementType.PlatformFee,
                Status = MoneyMovementStatus.Posted,
                AmountVnd = outcome.PlatformShareVnd,
                Reference = $"booking:{booking.Id}:cancel-platform",
                PostedAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }
    }

    public void Notify(Guid userId, string type, string title, string body, Booking booking, DateTimeOffset now) =>
        dbContext.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            DataJson = $"{{\"bookingId\":\"{booking.Id}\"}}",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

    public async Task<Guid?> FindProviderUserIdAsync(Guid providerId, CancellationToken cancellationToken) =>
        await dbContext.FreelancerProfiles.Where(x => x.ProviderId == providerId)
            .Select(x => (Guid?)x.UserId).SingleOrDefaultAsync(cancellationToken);

    /// <summary>Gửi yêu cầu hoàn tới cổng thanh toán; gọi sau khi transaction DB đã commit.</summary>
    public async Task SendRefundAsync(PendingGatewayRefund? refund, CancellationToken cancellationToken)
    {
        if (refund is null)
            return;
        try
        {
            await paymentGateway.RefundAsync(refund.ProviderTransactionId, refund.AmountVnd, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Sổ cái đã ghi nhận khoản hoàn; lỗi cổng cần đối soát thủ công.
            logger.LogError(exception, "Gateway refund failed for transaction {ProviderTransactionId}", refund.ProviderTransactionId);
        }
    }

    /// <summary>
    /// Chuyển đơn quá hạn sang Expired trong transaction riêng: hold hết hạn (chưa cọc) hoặc freelancer không phản hồi (hoàn đủ cọc).
    /// Chạy lặp an toàn; trả <c>false</c> nếu đơn không còn quá hạn sau khi khóa.
    /// </summary>
    public async Task<bool> ExpireIfDueAsync(Guid bookingId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        PendingGatewayRefund? refund = null;
        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var booking = await LockAsync(bookingId, cancellationToken);
            if (booking is null)
                return false;

            if (booking.Status == BookingStatus.PendingDeposit && booking.HoldExpiresAtUtc < now)
            {
                Transition(booking, BookingStatus.Expired, null, BookingTimelineReasons.HoldExpired, now);
                await CancelPendingDepositPaymentsAsync(booking.Id, now, cancellationToken);
                Notify(booking.CustomerUserId, "booking.expired", "Đơn đã hết hạn giữ chỗ",
                    $"Đơn {booking.BookingNumber} đã hết thời gian thanh toán cọc.", booking, now);
            }
            else if (booking.Status == BookingStatus.PendingProviderConfirmation && booking.ProviderConfirmationDeadlineUtc < now)
            {
                Transition(booking, BookingStatus.Expired, null, BookingTimelineReasons.ConfirmationExpired, now);
                refund = await RefundDepositAsync(booking, now, cancellationToken);
                Notify(booking.CustomerUserId, "booking.expired", "Đơn đã hết hạn xác nhận",
                    $"Đơn {booking.BookingNumber} không được xác nhận kịp, tiền cọc được hoàn đủ.", booking, now);
                if (await FindProviderUserIdAsync(booking.ProviderId, cancellationToken) is { } providerUserId)
                    Notify(providerUserId, "booking.expired", "Đơn đã hết hạn xác nhận",
                        $"Đơn {booking.BookingNumber} đã hết hạn do chưa được xác nhận.", booking, now);
            }
            else
            {
                return false;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await SendRefundAsync(refund, cancellationToken);
        return true;
    }

    public static string DepositHeldReference(Guid bookingId) => $"booking:{bookingId}:deposit-held";

    private async Task CloseDepositHeldAsync(Guid bookingId, MoneyMovementStatus status, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var reference = DepositHeldReference(bookingId);
        var held = dbContext.MoneyMovements.Local.SingleOrDefault(x => x.Reference == reference)
            ?? await dbContext.MoneyMovements.SingleOrDefaultAsync(x => x.Reference == reference, cancellationToken);
        if (held is null)
            return;
        held.Status = status;
        held.PostedAtUtc = status == MoneyMovementStatus.Posted ? now : held.PostedAtUtc;
        held.UpdatedAtUtc = now;
    }
}
