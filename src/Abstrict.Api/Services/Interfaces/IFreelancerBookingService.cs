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
    Task<FreelancerBookingDetailResponse> CheckInAsync(Guid userId, Guid bookingId, string token, CancellationToken cancellationToken);
    Task<FreelancerBookingDetailResponse> UpdateTaskStatusAsync(
        Guid userId, Guid bookingId, Guid taskId, BookingTaskStatus status, CancellationToken cancellationToken);
    Task<FreelancerBookingDetailResponse> CompleteWorkAsync(Guid userId, Guid bookingId, CancellationToken cancellationToken);
    Task<FreelancerBookingDetailResponse> UploadPhotoAsync(
        Guid userId, Guid bookingId, BookingPhotoType type, Guid? bookingTaskId, string? caption, long length, Stream content, CancellationToken cancellationToken);
    Task<BookingPhotoContent?> OpenPhotoAsync(Guid userId, Guid bookingId, Guid photoId, CancellationToken cancellationToken);
}
