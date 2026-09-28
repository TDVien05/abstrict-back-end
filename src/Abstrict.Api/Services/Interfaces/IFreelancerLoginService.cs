using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;

namespace Abstrict.Api.Services.Interfaces;

public interface IFreelancerLoginService
{
    Task<FreelancerLoginResponse?> LoginAsync(FreelancerLoginRequest request, CancellationToken cancellationToken);
}
