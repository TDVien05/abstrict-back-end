using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Services.Implementations;

public sealed class FreelancerDirectoryService(AppDbContext dbContext) : IFreelancerDirectoryService
{
    public const int MaximumPageSize = 50;

    public async Task<PagedResponse<FreelancerSummaryResponse>> SearchAsync(
        string? serviceCode, string? city, string? district, double? minRating, bool? acceptingBookings,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);

        var query = dbContext.FreelancerProfiles.AsNoTracking()
            .Where(f => f.Provider.Type == ProviderType.Freelancer && f.Provider.ApprovalStatus == ApprovalStatus.Approved);

        if (!string.IsNullOrWhiteSpace(serviceCode))
        {
            var code = serviceCode.Trim().ToUpperInvariant();
            query = query.Where(f => f.Provider.Services.Any(s => s.IsActive && s.ServiceCategory.Code == code));
        }
        if (!string.IsNullOrWhiteSpace(city))
        {
            var value = city.Trim();
            query = query.Where(f => f.Provider.ServiceAreas.Any(a => a.ServiceArea.City == value));
        }
        if (!string.IsNullOrWhiteSpace(district))
        {
            var value = district.Trim();
            query = query.Where(f => f.Provider.ServiceAreas.Any(a => a.ServiceArea.District == value));
        }
        if (minRating is { } rating)
            query = query.Where(f => f.Provider.AverageRating >= rating);
        if (acceptingBookings is { } accepting)
            query = query.Where(f => f.Provider.IsAcceptingBookings == accepting);

        var total = await query.LongCountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(f => f.Provider.AverageRating)
            .ThenByDescending(f => f.Provider.CompletedBookingCount)
            .ThenBy(f => f.ProviderId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new
            {
                f.ProviderId,
                f.Provider.DisplayName,
                f.Provider.Bio,
                f.Provider.AverageRating,
                f.Provider.RatingCount,
                f.Provider.CompletedBookingCount,
                f.ExperienceYears,
                f.Provider.IsAcceptingBookings,
                Services = f.Provider.Services.Where(s => s.IsActive)
                    .Select(s => new { s.ServiceCategory.Code, s.HourlyRateVnd }).ToList()
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(x => new FreelancerSummaryResponse(
            x.ProviderId, x.DisplayName, x.Bio, x.AverageRating, x.RatingCount, x.CompletedBookingCount,
            x.ExperienceYears, x.IsAcceptingBookings,
            x.Services.Count == 0 ? null : x.Services.Min(s => s.HourlyRateVnd),
            x.Services.Select(s => s.Code).Order().ToList())).ToList();

        return new PagedResponse<FreelancerSummaryResponse>(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<FreelancerDetailResponse?> GetAsync(Guid providerId, CancellationToken cancellationToken)
    {
        var row = await dbContext.FreelancerProfiles.AsNoTracking()
            .Where(f => f.ProviderId == providerId
                && f.Provider.Type == ProviderType.Freelancer
                && f.Provider.ApprovalStatus == ApprovalStatus.Approved)
            .Select(f => new
            {
                f.ProviderId,
                f.Provider.DisplayName,
                f.Provider.Bio,
                f.Gender,
                f.Provider.AverageRating,
                f.Provider.RatingCount,
                f.Provider.CompletedBookingCount,
                f.ExperienceYears,
                f.Provider.IsAcceptingBookings,
                Services = f.Provider.Services.Where(s => s.IsActive)
                    .OrderBy(s => s.ServiceCategory.Name)
                    .Select(s => new FreelancerServiceResponse(s.ServiceCategory.Code, s.ServiceCategory.Name, s.HourlyRateVnd, s.Description)).ToList(),
                Areas = f.Provider.ServiceAreas
                    .OrderBy(a => a.ServiceArea.City).ThenBy(a => a.ServiceArea.District)
                    .Select(a => new FreelancerServiceAreaResponse(a.ServiceAreaId, a.ServiceArea.City, a.ServiceArea.District, a.ServiceArea.WardOrComplex, a.TravelRadiusKm)).ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new FreelancerDetailResponse(row.ProviderId, row.DisplayName, row.Bio, row.Gender.ToString(), row.AverageRating,
                row.RatingCount, row.CompletedBookingCount, row.ExperienceYears, row.IsAcceptingBookings, row.Services, row.Areas);
    }
}
