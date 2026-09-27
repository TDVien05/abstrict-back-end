using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;

namespace Abstrict.Api.Services.Interfaces;

public interface ICustomerRegistrationService
{
    Task<CustomerRegistrationResponse> RegisterAsync(RegisterCustomerRequest request, CancellationToken cancellationToken);
    Task<CustomerPhoneVerificationResponse> VerifyPhoneAsync(VerifyCustomerPhoneOtpRequest request, CancellationToken cancellationToken);
    Task<CustomerRegistrationResponse?> ResendOtpAsync(ResendCustomerPhoneOtpRequest request, CancellationToken cancellationToken);
}
