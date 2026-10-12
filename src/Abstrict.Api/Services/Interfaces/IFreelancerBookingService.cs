using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Services.Interfaces;

public interface IFreelancerBookingService
{
    Task<PagedResponse<FreelancerBookingSummaryResponse>> ListAsync(Guid userId, BookingStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<FreelancerBookingCountsResponse> CountAsync(Guid userId, CancellationToken cancellationToken);
    Task<FreelancerBookingDetailResponse> GetAsync(Guid userId, Guid bookingId, CancellationToken cancellationToken);
    Task<FreelancerBookingDetailResponse> ConfirmAsync(Guid userId, Guid bookingId, CancellationToken cancellationToken);
    Task<FreelancerBookingDetailResponse> RejectAsync(Guid userId, Guid bookingId, string? reason, CancellationToken cancellationToken);
}
