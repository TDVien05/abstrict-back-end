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

public sealed class PromotionPurchaseService(
    AppDbContext dbContext, IPaymentGateway paymentGateway, TimeProvider timeProvider, IOptions<PaymentOptions> paymentOptions) : IPromotionPurchaseService
{
    private readonly PaymentOptions _payments = paymentOptions.Value;

    public async Task<PromotionPurchaseResponse> PurchaseAsync(
        Guid userId, Guid planId, PaymentMethod method, string? idempotencyKey, CancellationToken cancellationToken)
    {
        idempotencyKey = idempotencyKey?.Trim();
        if (string.IsNullOrEmpty(idempotencyKey) || idempotencyKey.Length > BookingPaymentService.MaximumIdempotencyKeyLength)
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "IDEMPOTENCY_KEY_REQUIRED",
                $"Cần header Idempotency-Key dài 1 đến {BookingPaymentService.MaximumIdempotencyKeyLength} ký tự.");
        var storedKey = $"{userId:N}:promotion:{idempotencyKey}";

        var providerId = await GetProviderIdAsync(userId, cancellationToken);

        var replay = await dbContext.PromotionPurchases.AsNoTracking()
            .Where(x => x.Payment!.IdempotencyKey == storedKey)
            .Select(x => new { x.Id, x.PromotionPlanId, x.ProviderId })
            .SingleOrDefaultAsync(cancellationToken);
        if (replay is not null)
            return replay.ProviderId == providerId && replay.PromotionPlanId == planId
                ? await GetAsync(replay.Id, cancellationToken)
                : throw new BookingFlowException(StatusCodes.Status409Conflict, "IDEMPOTENCY_KEY_REUSED", "Idempotency-Key này đã được dùng cho một lượt mua khác.");

        var plan = await dbContext.PromotionPlans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == planId, cancellationToken)
            ?? throw new BookingFlowException(StatusCodes.Status404NotFound, "PLAN_NOT_FOUND", "Không tìm thấy gói.");

        // Gọi cổng trước khi mở transaction; nếu bước ghi bên dưới bị từ chối thì giao dịch khởi tạo này bị bỏ.
        var paymentId = Guid.NewGuid();
        var initiation = plan.PriceVnd > 0 ? await paymentGateway.CreateDepositAsync(paymentId, plan.PriceVnd, method, cancellationToken) : null;

        var now = timeProvider.GetUtcNow();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var provider = await LockProviderAsync(providerId, cancellationToken);
        PromotionPurchasePolicy.EnsureCanPurchase(provider.ApprovalStatus);
        var current = await dbContext.PromotionPlans.AsNoTracking().SingleAsync(x => x.Id == planId, cancellationToken);
        PromotionPurchasePolicy.EnsurePlanAvailable(current.IsActive, current.Audience, provider.Type);

        var pending = await dbContext.PromotionPurchases.AsNoTracking().FirstOrDefaultAsync(
            x => x.ProviderId == providerId && x.PromotionPlanId == planId && x.Status == PromotionPurchaseStatus.PendingPayment, cancellationToken);
        if (pending is not null)
            return await GetAsync(pending.Id, cancellationToken);

        var purchase = new PromotionPurchase
        {
            ProviderId = providerId,
            PromotionPlanId = planId,
            PriceSnapshotVnd = current.PriceVnd,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        if (initiation is null)
        {
            await ActivateAsync(purchase, current.DurationDays, now, cancellationToken);
        }
        else
        {
            var payment = new Payment
            {
                Id = paymentId,
                PayerUserId = userId,
                Purpose = PaymentPurpose.Promotion,
                Method = method,
                Status = PaymentStatus.Pending,
                AmountVnd = current.PriceVnd,
                IdempotencyKey = storedKey,
                ProviderName = initiation.ProviderName,
                ProviderTransactionId = initiation.ProviderTransactionId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            dbContext.Payments.Add(payment);
            purchase.Payment = payment;
        }

        dbContext.PromotionPurchases.Add(purchase);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(purchase.Id, cancellationToken);
    }

    public async Task<PromotionPurchaseResponse> ConfirmSandboxAsync(Guid userId, Guid purchaseId, bool succeeded, CancellationToken cancellationToken)
    {
        if (!_payments.SandboxEnabled)
            throw PurchaseNotFound();

        var providerId = await GetProviderIdAsync(userId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockProviderAsync(providerId, cancellationToken);

        var purchase = await dbContext.PromotionPurchases.Include(x => x.Payment).Include(x => x.PromotionPlan)
            .SingleOrDefaultAsync(x => x.Id == purchaseId && x.ProviderId == providerId, cancellationToken)
            ?? throw PurchaseNotFound();
        var payment = purchase.Payment;
        if (payment is null || payment.ProviderName != SandboxPaymentGateway.Name)
            throw PurchaseNotFound();

        // Gọi lặp lại sau khi đã chốt thì không đổi gì.
        if (purchase.Status == PromotionPurchaseStatus.PendingPayment && payment.Status is PaymentStatus.Pending or PaymentStatus.Processing)
        {
            payment.UpdatedAtUtc = now;
            purchase.UpdatedAtUtc = now;
            if (succeeded)
            {
                payment.Status = PaymentStatus.Succeeded;
                payment.PaidAtUtc = now;
                await ActivateAsync(purchase, purchase.PromotionPlan.DurationDays, now, cancellationToken);
                dbContext.MoneyMovements.Add(new MoneyMovement
                {
                    PaymentId = payment.Id,
                    Type = MoneyMovementType.PromotionCharge,
                    Status = MoneyMovementStatus.Posted,
                    AmountVnd = payment.AmountVnd,
                    Reference = $"promotion:{purchase.Id}:charge",
                    PostedAtUtc = now,
                    Note = purchase.PromotionPlan.Code,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            }
            else
            {
                payment.Status = PaymentStatus.Failed;
                purchase.Status = PromotionPurchaseStatus.PaymentFailed;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return await GetAsync(purchaseId, cancellationToken);
    }

    public async Task<IReadOnlyList<PromotionPurchaseResponse>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var providerId = await GetProviderIdAsync(userId, cancellationToken);
        var rows = await Query().Where(x => x.ProviderId == providerId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        return rows.Select(x => ToResponse(x, now)).ToList();
    }

    /// <summary>Gói mới nối tiếp sau gói đang/sắp hiệu lực của cùng đối tác.</summary>
    private async Task ActivateAsync(PromotionPurchase purchase, int durationDays, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var latestEnd = await dbContext.PromotionPurchases.AsNoTracking()
            .Where(x => x.ProviderId == purchase.ProviderId && x.Status == PromotionPurchaseStatus.Active && x.EndsAtUtc > now)
            .MaxAsync(x => x.EndsAtUtc, cancellationToken);
        var (start, end) = PromotionPurchasePolicy.ComputeWindow(now, latestEnd, durationDays);
        purchase.Status = PromotionPurchaseStatus.Active;
        purchase.StartsAtUtc = start;
        purchase.EndsAtUtc = end;
    }

    private IQueryable<PromotionPurchase> Query() =>
        dbContext.PromotionPurchases.AsNoTracking().Include(x => x.PromotionPlan).Include(x => x.Payment);

    private async Task<PromotionPurchaseResponse> GetAsync(Guid purchaseId, CancellationToken cancellationToken) =>
        ToResponse(await Query().SingleAsync(x => x.Id == purchaseId, cancellationToken), timeProvider.GetUtcNow());

    private static PromotionPurchaseResponse ToResponse(PromotionPurchase x, DateTimeOffset now) => new(
        x.Id, x.PromotionPlanId, x.PromotionPlan.Code, x.PromotionPlan.Name, x.PromotionPlan.DurationDays, x.PriceSnapshotVnd,
        PromotionPurchasePolicy.EffectiveStatus(x.Status, x.EndsAtUtc, now), PromotionPurchasePolicy.IsCurrent(x.Status, x.StartsAtUtc, x.EndsAtUtc, now),
        x.StartsAtUtc, x.EndsAtUtc, x.PaymentId, x.Payment?.Method, x.CreatedAtUtc);

    /// <summary>Freelancer qua <c>FreelancerProfile.UserId</c>, doanh nghiệp qua <c>CompanyProfile.OwnerUserId</c>.</summary>
    private async Task<Guid> GetProviderIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var freelancer = await dbContext.FreelancerProfiles.AsNoTracking().Where(x => x.UserId == userId).Select(x => (Guid?)x.ProviderId).SingleOrDefaultAsync(cancellationToken);
        if (freelancer is { } id)
            return id;
        return await dbContext.CompanyProfiles.AsNoTracking().Where(x => x.OwnerUserId == userId).Select(x => (Guid?)x.ProviderId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new BookingFlowException(StatusCodes.Status404NotFound, "PROVIDER_NOT_FOUND", "Không tìm thấy hồ sơ đối tác.");
    }

    private async Task<Provider> LockProviderAsync(Guid providerId, CancellationToken cancellationToken)
    {
        await dbContext.Providers.FromSqlInterpolated($"SELECT * FROM providers WHERE id = {providerId} FOR UPDATE").ToListAsync(cancellationToken);
        return await dbContext.Providers.AsNoTracking().SingleAsync(x => x.Id == providerId, cancellationToken);
    }

    private static BookingFlowException PurchaseNotFound() => new(StatusCodes.Status404NotFound, "PURCHASE_NOT_FOUND", "Không tìm thấy lượt mua gói.");
}
