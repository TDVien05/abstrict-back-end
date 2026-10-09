using Abstrict.Api.DTOs.Responses;

namespace Abstrict.Api.Services.Interfaces;

public interface IFreelancerDirectoryService
{
    /// <summary>Chỉ trả freelancer đã được duyệt (Approved).</summary>
    Task<PagedResponse<FreelancerSummaryResponse>> SearchAsync(
        string? serviceCode, string? city, string? district, double? minRating, bool? acceptingBookings,
        int page, int pageSize, CancellationToken cancellationToken);

    Task<FreelancerDetailResponse?> GetAsync(Guid providerId, CancellationToken cancellationToken);
}
