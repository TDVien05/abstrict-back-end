using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;

namespace Abstrict.Api.Services.Interfaces;

public interface IAvailabilityService
{
    Task<IReadOnlyList<AvailabilityWindowResponse>> ListMineAsync(Guid userId, DateOnly? from, int days, CancellationToken cancellationToken);
    Task<AvailabilityWindowResponse> CreateAsync(Guid userId, CreateAvailabilityWindowRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, Guid windowId, CancellationToken cancellationToken);

    /// <summary>Lịch rảnh/bận công khai của một freelancer đã duyệt; trả <c>null</c> nếu không tìm thấy.</summary>
    Task<ProviderAvailabilityResponse?> GetPublicAsync(Guid providerId, DateOnly? from, int days, CancellationToken cancellationToken);
}
