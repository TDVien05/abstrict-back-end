using System.Text.Json;
using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Services.Implementations;

public sealed class FreelancerProfileService(AppDbContext dbContext, TimeProvider timeProvider) : IFreelancerProfileService
{
    public async Task<MyFreelancerProfileResponse> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        await BuildAsync(await GetProviderIdAsync(userId, cancellationToken), paused: false, cancellationToken);

    public async Task<MyFreelancerProfileResponse> UpdateAsync(Guid userId, UpdateFreelancerProfileRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var provider = await LockProviderAsync(userId, cancellationToken);
        var profile = provider.FreelancerProfile!;
        var before = new { provider.Bio, provider.IsAcceptingBookings, profile.AutoAcceptBookings };

        if (request.Bio is not null)
            provider.Bio = FreelancerProfilePolicy.NormalizeText(request.Bio);
        if (request.AutoAcceptBookings is { } autoAccept)
            profile.AutoAcceptBookings = autoAccept;
        if (request.IsAcceptingBookings is { } accepting)
        {
            if (accepting)
                FreelancerProfilePolicy.EnsureCanAcceptBookings(await MissingAsync(provider, cancellationToken));
            provider.IsAcceptingBookings = accepting;
        }

        var now = timeProvider.GetUtcNow();
        provider.UpdatedAtUtc = now;
        profile.UpdatedAtUtc = now;
        AddAudit(userId, "freelancer.profile_updated", provider.Id, before, new { provider.Bio, provider.IsAcceptingBookings, profile.AutoAcceptBookings });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await BuildAsync(provider.Id, paused: false, cancellationToken);
    }

    public async Task<MyFreelancerProfileResponse> SaveServiceAsync(
        Guid userId, Guid serviceCategoryId, SaveFreelancerServiceRequest request, CancellationToken cancellationToken)
    {
        var hourlyRate = request.HourlyRateVnd ?? 0;
        FreelancerProfilePolicy.EnsureValidHourlyRate(hourlyRate);
        var description = FreelancerProfilePolicy.NormalizeText(request.Description);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var provider = await LockProviderAsync(userId, cancellationToken);
        var category = await dbContext.ServiceCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == serviceCategoryId, cancellationToken)
            ?? throw new BookingFlowException(StatusCodes.Status404NotFound, "SERVICE_CATEGORY_NOT_FOUND", "Không tìm thấy danh mục dịch vụ.");
        if (request.IsActive && !category.IsActive)
            throw new BookingFlowException(StatusCodes.Status409Conflict, "SERVICE_CATEGORY_INACTIVE", "Danh mục dịch vụ này đã ngừng hoạt động.");

        var now = timeProvider.GetUtcNow();
        var service = await dbContext.ProviderServices.SingleOrDefaultAsync(
            x => x.ProviderId == provider.Id && x.ServiceCategoryId == serviceCategoryId, cancellationToken);
        var before = service is null ? null : new { category.Code, service.HourlyRateVnd, service.Description, service.IsActive };
        if (service is null)
        {
            service = new ProviderService { ProviderId = provider.Id, ServiceCategoryId = serviceCategoryId, CreatedAtUtc = now };
            dbContext.ProviderServices.Add(service);
        }

        service.HourlyRateVnd = hourlyRate;
        service.Description = description;
        service.IsActive = request.IsActive;
        service.UpdatedAtUtc = now;
        AddAudit(userId, "freelancer.service_saved", provider.Id, before, new { category.Code, service.HourlyRateVnd, service.Description, service.IsActive });
        await dbContext.SaveChangesAsync(cancellationToken);

        return await FinishAsync(provider, transaction, cancellationToken);
    }

    public async Task<MyFreelancerProfileResponse> RemoveServiceAsync(Guid userId, Guid serviceCategoryId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var provider = await LockProviderAsync(userId, cancellationToken);
        var service = await dbContext.ProviderServices.Include(x => x.ServiceCategory).SingleOrDefaultAsync(
            x => x.ProviderId == provider.Id && x.ServiceCategoryId == serviceCategoryId, cancellationToken)
            ?? throw new BookingFlowException(StatusCodes.Status404NotFound, "SERVICE_NOT_FOUND", "Bạn chưa cung cấp dịch vụ này.");

        // Đơn đã đặt giữ nguyên giá tại thời điểm đặt nên xóa kỹ năng không ảnh hưởng các đơn đó.
        AddAudit(userId, "freelancer.service_removed", provider.Id, new { service.ServiceCategory.Code, service.HourlyRateVnd, service.Description, service.IsActive }, null);
        dbContext.ProviderServices.Remove(service);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await FinishAsync(provider, transaction, cancellationToken);
    }

    public async Task<MyFreelancerProfileResponse> ReplaceServiceAreasAsync(
        Guid userId, ReplaceFreelancerServiceAreasRequest request, CancellationToken cancellationToken)
    {
        var requested = FreelancerProfilePolicy.NormalizeServiceAreas(request.ServiceAreas);
        var requestedIds = requested.Select(x => x.ServiceAreaId).ToList();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var provider = await LockProviderAsync(userId, cancellationToken);
        var validCount = await dbContext.ServiceAreas.CountAsync(x => requestedIds.Contains(x.Id) && x.IsActive, cancellationToken);
        if (validCount != requested.Count)
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "INVALID_SERVICE_AREA", "Có khu vực không tồn tại hoặc đã ngừng hoạt động.");

        var now = timeProvider.GetUtcNow();
        var existing = await dbContext.ProviderServiceAreas.Where(x => x.ProviderId == provider.Id).ToListAsync(cancellationToken);
        var before = existing.Select(x => new { x.ServiceAreaId, x.TravelRadiusKm }).ToList();

        dbContext.ProviderServiceAreas.RemoveRange(existing.Where(x => !requestedIds.Contains(x.ServiceAreaId)));
        foreach (var area in requested)
        {
            var current = existing.SingleOrDefault(x => x.ServiceAreaId == area.ServiceAreaId);
            if (current is null)
            {
                dbContext.ProviderServiceAreas.Add(new ProviderServiceArea
                {
                    ProviderId = provider.Id,
                    ServiceAreaId = area.ServiceAreaId,
                    TravelRadiusKm = area.TravelRadiusKm,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            }
            else if (current.TravelRadiusKm != area.TravelRadiusKm)
            {
                current.TravelRadiusKm = area.TravelRadiusKm;
                current.UpdatedAtUtc = now;
            }
        }

        AddAudit(userId, "freelancer.service_areas_replaced", provider.Id, before, requested.Select(x => new { x.ServiceAreaId, TravelRadiusKm = (int?)x.TravelRadiusKm }).ToList());
        await dbContext.SaveChangesAsync(cancellationToken);

        return await FinishAsync(provider, transaction, cancellationToken);
    }

    /// <summary>Sau khi đổi dịch vụ/khu vực: nếu hồ sơ không còn đủ điều kiện nhận đơn thì tự tắt nhận đơn rồi mới chốt giao dịch.</summary>
    private async Task<MyFreelancerProfileResponse> FinishAsync(
        Provider provider, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        var paused = false;
        if (provider.IsAcceptingBookings && (await MissingAsync(provider, cancellationToken)).Count > 0)
        {
            provider.IsAcceptingBookings = false;
            provider.UpdatedAtUtc = timeProvider.GetUtcNow();
            AddAudit(null, "freelancer.accepting_bookings_paused", provider.Id, new { IsAcceptingBookings = true }, new { IsAcceptingBookings = false });
            await dbContext.SaveChangesAsync(cancellationToken);
            paused = true;
        }

        await transaction.CommitAsync(cancellationToken);
        return await BuildAsync(provider.Id, paused, cancellationToken);
    }

    private async Task<Guid> GetProviderIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.FreelancerProfiles.AsNoTracking()
            .Where(x => x.UserId == userId && x.Provider.Type == ProviderType.Freelancer)
            .Select(x => (Guid?)x.ProviderId)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new BookingFlowException(StatusCodes.Status404NotFound, "FREELANCER_PROFILE_NOT_FOUND", "Không tìm thấy hồ sơ freelancer.");

    /// <summary>Khóa dòng provider để các lần sửa đồng thời của cùng một freelancer chạy tuần tự, rồi nạp hồ sơ để theo dõi thay đổi.</summary>
    private async Task<Provider> LockProviderAsync(Guid userId, CancellationToken cancellationToken)
    {
        var providerId = await GetProviderIdAsync(userId, cancellationToken);
        await dbContext.Providers.FromSqlInterpolated($"SELECT * FROM providers WHERE id = {providerId} FOR UPDATE").ToListAsync(cancellationToken);
        var provider = await dbContext.Providers.Include(x => x.FreelancerProfile).SingleAsync(x => x.Id == providerId, cancellationToken);
        FreelancerProfilePolicy.EnsureEditable(provider.ApprovalStatus);
        return provider;
    }

    private async Task<IReadOnlyList<string>> MissingAsync(Provider provider, CancellationToken cancellationToken)
    {
        var services = await dbContext.ProviderServices.CountAsync(
            x => x.ProviderId == provider.Id && x.IsActive && x.ServiceCategory.IsActive, cancellationToken);
        var areas = await dbContext.ProviderServiceAreas.CountAsync(x => x.ProviderId == provider.Id && x.ServiceArea.IsActive, cancellationToken);
        return FreelancerProfilePolicy.MissingForBookings(provider.ApprovalStatus, services, areas);
    }

    private async Task<MyFreelancerProfileResponse> BuildAsync(Guid providerId, bool paused, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        var profile = await dbContext.FreelancerProfiles.AsNoTracking()
            .Where(x => x.ProviderId == providerId)
            .Select(x => new { x.ProviderId, x.Provider.DisplayName, x.Provider.ApprovalStatus, x.Provider.Bio, x.ExperienceYears, x.Provider.IsAcceptingBookings, x.AutoAcceptBookings })
            .SingleAsync(cancellationToken);
        var services = await dbContext.ProviderServices.AsNoTracking()
            .Where(x => x.ProviderId == providerId)
            .OrderBy(x => x.ServiceCategory.Name)
            .Select(x => new MyFreelancerServiceResponse(
                x.ServiceCategoryId, x.ServiceCategory.Code, x.ServiceCategory.Name, x.HourlyRateVnd, x.Description, x.IsActive, x.IsActive && x.ServiceCategory.IsActive))
            .ToListAsync(cancellationToken);
        var areas = await dbContext.ProviderServiceAreas.AsNoTracking()
            .Where(x => x.ProviderId == providerId)
            .OrderBy(x => x.ServiceArea.City).ThenBy(x => x.ServiceArea.District)
            .Select(x => new MyFreelancerServiceAreaResponse(x.ServiceAreaId, x.ServiceArea.City, x.ServiceArea.District, x.ServiceArea.WardOrComplex, x.TravelRadiusKm, x.ServiceArea.IsActive))
            .ToListAsync(cancellationToken);

        var missing = FreelancerProfilePolicy.MissingForBookings(profile.ApprovalStatus, services.Count(x => x.IsListed), areas.Count(x => x.IsActive));
        return new MyFreelancerProfileResponse(
            profile.ProviderId, profile.DisplayName, profile.ApprovalStatus, profile.Bio, profile.ExperienceYears, profile.IsAcceptingBookings,
            profile.AutoAcceptBookings, missing.Count == 0, missing, paused, services, areas);
    }

    private void AddAudit(Guid? actorUserId, string action, Guid providerId, object? before, object? after) =>
        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = action,
            EntityType = nameof(Provider),
            EntityId = providerId,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after)
        });
}
