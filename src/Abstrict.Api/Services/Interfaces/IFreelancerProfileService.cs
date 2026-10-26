using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;

namespace Abstrict.Api.Services.Interfaces;

/// <summary>Freelancer tự quản lý hồ sơ công khai. Mọi thao tác ghi đều trả về hồ sơ mới nhất.</summary>
public interface IFreelancerProfileService
{
    Task<MyFreelancerProfileResponse> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task<MyFreelancerProfileResponse> UpdateAsync(Guid userId, UpdateFreelancerProfileRequest request, CancellationToken cancellationToken);

    /// <summary>Thêm hoặc sửa kỹ năng (dịch vụ) và giá theo giờ.</summary>
    Task<MyFreelancerProfileResponse> SaveServiceAsync(Guid userId, Guid serviceCategoryId, SaveFreelancerServiceRequest request, CancellationToken cancellationToken);

    Task<MyFreelancerProfileResponse> RemoveServiceAsync(Guid userId, Guid serviceCategoryId, CancellationToken cancellationToken);
    Task<MyFreelancerProfileResponse> ReplaceServiceAreasAsync(Guid userId, ReplaceFreelancerServiceAreasRequest request, CancellationToken cancellationToken);
}
