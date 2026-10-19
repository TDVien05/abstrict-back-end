using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;

namespace Abstrict.Api.Services.Interfaces;

public interface IRatingService
{
    Task<RatingResponse> CreateProviderRatingAsync(Guid customerUserId, Guid bookingId, CreateProviderRatingRequest request, CancellationToken cancellationToken);
    Task<RatingResponse> GetCustomerRatingAsync(Guid customerUserId, Guid bookingId, CancellationToken cancellationToken);
    Task<PagedResponse<PublicRatingResponse>> ListProviderRatingsAsync(Guid providerId, int page, int pageSize, CancellationToken cancellationToken);
}
