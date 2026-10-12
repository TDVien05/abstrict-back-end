using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;

namespace Abstrict.Api.Services.Interfaces;

public interface ICustomerAddressService
{
    Task<IReadOnlyList<CustomerAddressResponse>> ListAsync(Guid userId, CancellationToken cancellationToken);
    Task<CustomerAddressResponse> CreateAsync(Guid userId, SaveCustomerAddressRequest request, CancellationToken cancellationToken);
    Task<CustomerAddressResponse> UpdateAsync(Guid userId, Guid addressId, SaveCustomerAddressRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, Guid addressId, CancellationToken cancellationToken);
}
