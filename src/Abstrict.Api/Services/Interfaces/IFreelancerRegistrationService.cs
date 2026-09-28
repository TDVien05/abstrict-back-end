using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;

namespace Abstrict.Api.Services.Interfaces;

public interface IFreelancerRegistrationService
{
    Task<FreelancerRegistrationResponse> RegisterAsync(RegisterFreelancerRequest request, CancellationToken cancellationToken);
    Task<FreelancerPhoneVerificationResponse> VerifyPhoneAsync(VerifyFreelancerPhoneOtpRequest request, CancellationToken cancellationToken);
    Task<FreelancerRegistrationResponse?> ResendOtpAsync(ResendFreelancerPhoneOtpRequest request, CancellationToken cancellationToken);
}
