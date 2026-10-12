using Abstrict.Api.DTOs.Responses;

namespace Abstrict.Api.Services.Interfaces;

public interface ICustomerProfileService
{
    Task<CustomerProfileResponse> GetAsync(Guid userId, CancellationToken cancellationToken);
}
