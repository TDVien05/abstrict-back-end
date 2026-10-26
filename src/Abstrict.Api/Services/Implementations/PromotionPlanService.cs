using System.Text.Json;
using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Abstrict.Api.Services.Implementations;

public sealed class PromotionPlanService(AppDbContext dbContext, TimeProvider timeProvider) : IPromotionPlanService
{
    public async Task<IReadOnlyList<AdminPromotionPlanResponse>> ListAdminAsync(
        PromotionAudience? audience, bool? isActive, CancellationToken cancellationToken)
    {
        var query = dbContext.PromotionPlans.AsNoTracking().AsQueryable();
        if (audience is { } wanted)
            query = query.Where(x => x.Audience == wanted);
        if (isActive is { } active)
            query = query.Where(x => x.IsActive == active);

        var rows = await query
            .OrderBy(x => x.Audience).ThenBy(x => x.DurationDays).ThenBy(x => x.PriceVnd).ThenBy(x => x.Code)
            .Select(x => new { Plan = x, Purchases = dbContext.PromotionPurchases.Count(p => p.PromotionPlanId == x.Id) })
            .ToListAsync(cancellationToken);
        return rows.Select(x => ToAdminResponse(x.Plan, x.Purchases)).ToList();
    }

    public async Task<PromotionReportResponse> GetReportAsync(CancellationToken cancellationToken)
    {
        const int days = 7;
        var now = timeProvider.GetUtcNow();
        var plans = await dbContext.PromotionPlans.AsNoTracking().ToListAsync(cancellationToken);
        var statusCounts = await dbContext.PromotionPurchases.AsNoTracking()
            .GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var perPlan = await dbContext.PromotionPurchases.AsNoTracking()
            .Where(x => x.Status == PromotionPurchaseStatus.Active)
            .GroupBy(x => x.PromotionPlanId)
            .Select(g => new
            {
                PlanId = g.Key,
                Purchases = g.Count(),
                ActiveNow = g.Count(x => x.StartsAtUtc <= now && x.EndsAtUtc > now),
                Revenue = g.Sum(x => x.PriceSnapshotVnd)
            })
            .ToListAsync(cancellationToken);

        // Lấy dư một ngày so với 7 ngày giờ Việt Nam; bộ gom ngày tự cắt lại cho đúng.
        var since = now.AddDays(-days - 1);
        var recent = (await dbContext.PromotionPurchases.AsNoTracking()
                .Where(x => x.Status == PromotionPurchaseStatus.Active && (x.Payment!.PaidAtUtc ?? x.CreatedAtUtc) >= since)
                .Select(x => new { At = x.Payment!.PaidAtUtc ?? x.CreatedAtUtc, x.PriceSnapshotVnd })
                .ToListAsync(cancellationToken))
            .Select(x => (x.At, x.PriceSnapshotVnd));
        var daily = PromotionReportPolicy.Daily(recent, now, days);

        var byPlan = perPlan.ToDictionary(x => x.PlanId);
        var rows = plans
            .Select(plan => byPlan.TryGetValue(plan.Id, out var stat)
                ? new PromotionPlanReportRow(plan.Id, plan.Code, plan.Name, plan.Audience, plan.IsActive, stat.Purchases, stat.ActiveNow, stat.Revenue)
                : new PromotionPlanReportRow(plan.Id, plan.Code, plan.Name, plan.Audience, plan.IsActive, 0, 0, 0))
            .OrderByDescending(x => x.RevenueVnd).ThenByDescending(x => x.Purchases).ThenBy(x => x.Code)
            .ToList();
        var audiences = rows.GroupBy(x => x.Audience)
            .Select(g => new PromotionAudienceReportRow(g.Key, g.Sum(x => x.Purchases), g.Sum(x => x.RevenueVnd)))
            .OrderBy(x => x.Audience).ToList();
        int Count(PromotionPurchaseStatus status) => statusCounts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        return new PromotionReportResponse(
            now, plans.Count(x => x.IsActive), plans.Count(x => !x.IsActive), Count(PromotionPurchaseStatus.Active), rows.Sum(x => x.ActiveNow),
            Count(PromotionPurchaseStatus.PendingPayment), Count(PromotionPurchaseStatus.PaymentFailed), rows.Sum(x => x.RevenueVnd),
            daily.Sum(x => x.RevenueVnd), daily.Select(x => new PromotionDailyReportRow(x.Date.ToString("yyyy-MM-dd"), x.Purchases, x.RevenueVnd)).ToList(),
            audiences, rows);
    }

    public async Task<AdminPromotionPlanResponse> GetAdminAsync(Guid planId, CancellationToken cancellationToken)
    {
        var row = await dbContext.PromotionPlans.AsNoTracking()
            .Where(x => x.Id == planId)
            .Select(x => new { Plan = x, Purchases = dbContext.PromotionPurchases.Count(p => p.PromotionPlanId == x.Id) })
            .SingleOrDefaultAsync(cancellationToken) ?? throw NotFound();
        return ToAdminResponse(row.Plan, row.Purchases);
    }

    public async Task<AdminPromotionPlanResponse> CreateAsync(Guid adminUserId, CreatePromotionPlanRequest request, CancellationToken cancellationToken)
    {
        var code = PromotionPlanPolicy.NormalizeCode(request.Code);
        var name = PromotionPlanPolicy.NormalizeName(request.Name);
        var audience = request.Audience ?? throw new BookingFlowException(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Thiếu đối tượng của gói.");
        var (duration, price) = RequiredNumbers(request);
        PromotionPlanPolicy.EnsureValidValues(audience, duration, price, request.PriorityRadiusKm, request.WorkerLimit);
        var benefits = PromotionPlanPolicy.NormalizeBenefits(request.Benefits);

        if (await dbContext.PromotionPlans.AnyAsync(x => x.Code == code, cancellationToken))
            throw CodeTaken();

        var now = timeProvider.GetUtcNow();
        var plan = new PromotionPlan
        {
            Code = code,
            Name = name,
            Audience = audience,
            DurationDays = duration,
            PriceVnd = price,
            PriorityRadiusKm = request.PriorityRadiusKm,
            WorkerLimit = request.WorkerLimit,
            BenefitsJson = PromotionPlanPolicy.SerializeBenefits(benefits),
            IsActive = request.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.PromotionPlans.Add(plan);
        AddAudit(adminUserId, "promotion_plan.created", plan.Id, null, Snapshot(plan));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw CodeTaken();
        }

        return ToAdminResponse(plan, 0);
    }

    public async Task<AdminPromotionPlanResponse> UpdateAsync(Guid adminUserId, Guid planId, UpdatePromotionPlanRequest request, CancellationToken cancellationToken)
    {
        var name = PromotionPlanPolicy.NormalizeName(request.Name);
        var (duration, price) = RequiredNumbers(request);
        var benefits = PromotionPlanPolicy.NormalizeBenefits(request.Benefits);

        var plan = await dbContext.PromotionPlans.SingleOrDefaultAsync(x => x.Id == planId, cancellationToken) ?? throw NotFound();
        PromotionPlanPolicy.EnsureValidValues(plan.Audience, duration, price, request.PriorityRadiusKm, request.WorkerLimit);

        var before = Snapshot(plan);
        plan.Name = name;
        plan.DurationDays = duration;
        plan.PriceVnd = price;
        plan.PriorityRadiusKm = request.PriorityRadiusKm;
        plan.WorkerLimit = request.WorkerLimit;
        plan.BenefitsJson = PromotionPlanPolicy.SerializeBenefits(benefits);
        plan.IsActive = request.IsActive;
        plan.UpdatedAtUtc = timeProvider.GetUtcNow();
        AddAudit(adminUserId, "promotion_plan.updated", plan.Id, before, Snapshot(plan));
        await dbContext.SaveChangesAsync(cancellationToken);

        var purchases = await dbContext.PromotionPurchases.CountAsync(x => x.PromotionPlanId == planId, cancellationToken);
        return ToAdminResponse(plan, purchases);
    }

    public async Task DeleteAsync(Guid adminUserId, Guid planId, CancellationToken cancellationToken)
    {
        var plan = await dbContext.PromotionPlans.SingleOrDefaultAsync(x => x.Id == planId, cancellationToken) ?? throw NotFound();
        if (await dbContext.PromotionPurchases.AnyAsync(x => x.PromotionPlanId == planId, cancellationToken))
            throw new BookingFlowException(StatusCodes.Status409Conflict, "PLAN_IN_USE", "Gói đã có lượt mua nên không xóa được, hãy tắt gói thay vì xóa.");

        AddAudit(adminUserId, "promotion_plan.deleted", plan.Id, Snapshot(plan), null);
        dbContext.PromotionPlans.Remove(plan);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            // Có lượt mua được tạo giữa lúc kiểm tra và lúc xóa.
            throw new BookingFlowException(StatusCodes.Status409Conflict, "PLAN_IN_USE", "Gói đã có lượt mua nên không xóa được, hãy tắt gói thay vì xóa.");
        }
    }

    public async Task<IReadOnlyList<PartnerPromotionPlanResponse>> ListForPartnerAsync(Guid userId, CancellationToken cancellationToken)
    {
        var role = await dbContext.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => (UserRole?)x.Role).SingleOrDefaultAsync(cancellationToken)
            ?? throw new BookingFlowException(StatusCodes.Status401Unauthorized, "INVALID_TOKEN", "Phiên đăng nhập không hợp lệ.");
        var audience = PromotionPlanPolicy.AudienceOf(role);

        var plans = await dbContext.PromotionPlans.AsNoTracking()
            .Where(x => x.IsActive && x.Audience == audience)
            .OrderBy(x => x.DurationDays).ThenBy(x => x.PriceVnd).ThenBy(x => x.Code)
            .ToListAsync(cancellationToken);
        return plans.Select(x => new PartnerPromotionPlanResponse(
            x.Id, x.Code, x.Name, x.Audience, x.DurationDays, x.PriceVnd, x.PriorityRadiusKm, x.WorkerLimit, PromotionPlanPolicy.ParseBenefits(x.BenefitsJson))).ToList();
    }

    private static (int Duration, long Price) RequiredNumbers(PromotionPlanFields request) =>
        (request.DurationDays ?? throw new BookingFlowException(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Thiếu thời hạn của gói."),
         request.PriceVnd ?? throw new BookingFlowException(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Thiếu giá của gói."));

    private static AdminPromotionPlanResponse ToAdminResponse(PromotionPlan plan, int purchaseCount) => new(
        plan.Id, plan.Code, plan.Name, plan.Audience, plan.DurationDays, plan.PriceVnd, plan.PriorityRadiusKm, plan.WorkerLimit,
        PromotionPlanPolicy.ParseBenefits(plan.BenefitsJson), plan.IsActive, purchaseCount, plan.CreatedAtUtc, plan.UpdatedAtUtc);

    private static object Snapshot(PromotionPlan plan) => new
    {
        plan.Code, plan.Name, Audience = plan.Audience.ToString(), plan.DurationDays, plan.PriceVnd, plan.PriorityRadiusKm,
        plan.WorkerLimit, Benefits = PromotionPlanPolicy.ParseBenefits(plan.BenefitsJson), plan.IsActive
    };

    private void AddAudit(Guid adminUserId, string action, Guid planId, object? before, object? after) =>
        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorUserId = adminUserId,
            Action = action,
            EntityType = nameof(PromotionPlan),
            EntityId = planId,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after)
        });

    private static BookingFlowException NotFound() => new(StatusCodes.Status404NotFound, "PLAN_NOT_FOUND", "Không tìm thấy gói.");

    private static BookingFlowException CodeTaken() => new(StatusCodes.Status409Conflict, "PLAN_CODE_TAKEN", "Mã gói đã được dùng cho một gói khác.");
}
