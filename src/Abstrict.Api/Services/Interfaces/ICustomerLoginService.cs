using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;

namespace Abstrict.Api.Services.Interfaces;

public interface ICustomerLoginService
{
    Task<CustomerLoginResponse?> LoginAsync(CustomerLoginRequest request, CancellationToken cancellationToken);
}
