using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Integrations.Payments;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Options;
using Abstrict.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Abstrict.Api.Services.Implementations;

public sealed class BookingPaymentService(
    AppDbContext dbContext, BookingWorkflow workflow, IPaymentGateway paymentGateway, TimeProvider timeProvider,
    IOptions<BookingOptions> bookingOptions, IOptions<PaymentOptions> paymentOptions) : IBookingPaymentService
{
    public const int MaximumIdempotencyKeyLength = 100;

    private readonly BookingOptions _booking = bookingOptions.Value;
    private readonly PaymentOptions _payments = paymentOptions.Value;

    public async Task<DepositPaymentResponse> InitiateDepositAsync(
        Guid userId, Guid bookingId, string? idempotencyKey, PaymentMethod method, CancellationToken cancellationToken)
    {
        idempotencyKey = idempotencyKey?.Trim();
        if (string.IsNullOrEmpty(idempotencyKey) || idempotencyKey.Length > MaximumIdempotencyKeyLength)
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "IDEMPOTENCY_KEY_REQUIRED",
                $"Cần header Idempotency-Key dài 1 đến {MaximumIdempotencyKeyLength} ký tự.");
        var storedKey = $"{userId:N}:{idempotencyKey}";

        var snapshot = await dbContext.Bookings.AsNoTracking()
            .Where(x => x.Id == bookingId && x.CustomerUserId == userId)
            .Select(x => new { x.DepositAmountVnd })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw BookingNotFound();

        var replay = await dbContext.Payments.AsNoTracking().SingleOrDefaultAsync(x => x.IdempotencyKey == storedKey, cancellationToken);
        if (replay is not null)
            return replay.BookingId == bookingId
                ? ToResponse(replay, await HoldExpiryAsync(bookingId, cancellationToken))
                : throw new BookingFlowException(StatusCodes.Status409Conflict, "IDEMPOTENCY_KEY_REUSED",
                    "Idempotency-Key này đã được dùng cho một đơn khác.");

        // Gọi cổng thanh toán trước khi mở transaction DB; nếu bước ghi bên dưới bị từ chối thì giao dịch khởi tạo này bị bỏ.
        var paymentId = Guid.NewGuid();
        var initiation = await paymentGateway.CreateDepositAsync(paymentId, snapshot.DepositAmountVnd, method, cancellationToken);

        var now = timeProvider.GetUtcNow();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var booking = await workflow.LockAsync(bookingId, cancellationToken);
        if (booking is null || booking.CustomerUserId != userId)
            throw BookingNotFound();
        if (booking.Status != BookingStatus.PendingDeposit)
            throw new BookingFlowException(StatusCodes.Status409Conflict, "BOOKING_INVALID_STATE", "Đơn không còn ở trạng thái chờ đặt cọc.");
        if (booking.HoldExpiresAtUtc < now)
            throw new BookingFlowException(StatusCodes.Status409Conflict, "HOLD_EXPIRED", "Đã hết thời gian giữ chỗ. Vui lòng đặt lại.");

        var pending = await dbContext.Payments.AsNoTracking().FirstOrDefaultAsync(
            x => x.BookingId == bookingId && x.Purpose == PaymentPurpose.Deposit && x.Status == PaymentStatus.Pending, cancellationToken);
        if (pending is not null)
            return ToResponse(pending, booking.HoldExpiresAtUtc);

        var payment = new Payment
        {
            Id = paymentId,
            BookingId = booking.Id,
            PayerUserId = userId,
            Purpose = PaymentPurpose.Deposit,
            Method = method,
            Status = PaymentStatus.Pending,
            AmountVnd = booking.DepositAmountVnd,
            IdempotencyKey = storedKey,
            ProviderName = initiation.ProviderName,
            ProviderTransactionId = initiation.ProviderTransactionId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(payment, booking.HoldExpiresAtUtc, initiation.CheckoutUrl);
    }

    public async Task<DepositPaymentResponse> ConfirmSandboxAsync(Guid userId, Guid paymentId, bool succeeded, CancellationToken cancellationToken)
    {
        if (!_payments.SandboxEnabled)
            throw PaymentNotFound();

        var payment = await dbContext.Payments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == paymentId && x.PayerUserId == userId && x.ProviderName == SandboxPaymentGateway.Name, cancellationToken);
        if (payment?.ProviderTransactionId is null)
            throw PaymentNotFound();

        await HandleGatewayResultAsync(SandboxPaymentGateway.Name, payment.ProviderTransactionId, succeeded, cancellationToken);

        dbContext.ChangeTracker.Clear();
        var updated = await dbContext.Payments.AsNoTracking().SingleAsync(x => x.Id == paymentId, cancellationToken);
        return ToResponse(updated, updated.BookingId is { } bookingId ? await HoldExpiryAsync(bookingId, cancellationToken) : null);
    }

    public async Task HandleGatewayResultAsync(string providerName, string providerTransactionId, bool succeeded, CancellationToken cancellationToken)
    {
        var reference = await dbContext.Payments.AsNoTracking()
            .Where(x => x.ProviderName == providerName && x.ProviderTransactionId == providerTransactionId && x.Purpose == PaymentPurpose.Deposit)
            .Select(x => new { x.Id, x.BookingId })
            .SingleOrDefaultAsync(cancellationToken);
        if (reference?.BookingId is not { } bookingId)
            throw PaymentNotFound();

        var now = timeProvider.GetUtcNow();
        PendingGatewayRefund? refund = null;

        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            // Khóa booking trước rồi mới đọc payment để hai lần gọi trùng nhau được xử lý tuần tự.
            var booking = await workflow.LockAsync(bookingId, cancellationToken) ?? throw PaymentNotFound();
            var payment = await dbContext.Payments.SingleAsync(x => x.Id == reference.Id, cancellationToken);
            if (payment.Status is not (PaymentStatus.Pending or PaymentStatus.Processing))
                return;

            payment.UpdatedAtUtc = now;
            if (!succeeded)
            {
                payment.Status = PaymentStatus.Failed;
            }
            else
            {
                payment.Status = PaymentStatus.Succeeded;
                payment.PaidAtUtc = now;
                workflow.RecordDepositHeld(booking, payment, now);

                if (booking.Status == BookingStatus.PendingDeposit && booking.HoldExpiresAtUtc < now)
                    workflow.Transition(booking, BookingStatus.Expired, null, BookingTimelineReasons.HoldExpired, now);

                if (booking.Status == BookingStatus.PendingDeposit)
                    await AcceptDepositAsync(booking, now, cancellationToken);
                else
                    // Tiền về sau khi đơn đã hết hạn/bị hủy: không hồi sinh đơn, hoàn ngay.
                    refund = await workflow.RefundPaymentAsync(booking, payment, now, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await workflow.SendRefundAsync(refund, cancellationToken);
    }

    private async Task AcceptDepositAsync(Booking booking, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var freelancer = await dbContext.FreelancerProfiles.AsNoTracking()
            .Where(x => x.ProviderId == booking.ProviderId)
            .Select(x => new { x.UserId, x.AutoAcceptBookings })
            .SingleOrDefaultAsync(cancellationToken);

        booking.HoldExpiresAtUtc = null;
        if (freelancer?.AutoAcceptBookings == true)
        {
            workflow.Transition(booking, BookingStatus.Confirmed, null, BookingTimelineReasons.AutoConfirmed, now);
            workflow.Notify(booking.CustomerUserId, "booking.confirmed", "Đơn đã được xác nhận",
                $"Đơn {booking.BookingNumber} đã được xác nhận.", booking, now);
        }
        else
        {
            workflow.Transition(booking, BookingStatus.PendingProviderConfirmation, booking.CustomerUserId, BookingTimelineReasons.DepositPaid, now);
            var deadline = now.AddMinutes(_booking.ProviderConfirmationMinutes);
            booking.ProviderConfirmationDeadlineUtc = deadline < booking.StartsAtUtc ? deadline : booking.StartsAtUtc;
        }

        if (freelancer is not null)
            workflow.Notify(freelancer.UserId, "booking.deposit_paid", "Có đơn mới đã đặt cọc",
                $"Đơn {booking.BookingNumber} đã được khách đặt cọc.", booking, now);
    }

    private async Task<DateTimeOffset?> HoldExpiryAsync(Guid bookingId, CancellationToken cancellationToken) =>
        await dbContext.Bookings.AsNoTracking().Where(x => x.Id == bookingId)
            .Select(x => x.HoldExpiresAtUtc).SingleOrDefaultAsync(cancellationToken);

    private static DepositPaymentResponse ToResponse(Payment payment, DateTimeOffset? expiresAtUtc, string? checkoutUrl = null) =>
        new(payment.Id, payment.Status, payment.AmountVnd, payment.Method, expiresAtUtc, checkoutUrl);

    private static BookingFlowException BookingNotFound() =>
        new(StatusCodes.Status404NotFound, "BOOKING_NOT_FOUND", "Không tìm thấy đơn đặt lịch.");

    private static BookingFlowException PaymentNotFound() =>
        new(StatusCodes.Status404NotFound, "PAYMENT_NOT_FOUND", "Không tìm thấy giao dịch thanh toán.");
}
