using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Abstrict.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Version1 + "/auth/freelancers")]
public sealed class FreelancerAuthController(
    IFreelancerRegistrationService registrationService,
    IFreelancerLoginService loginService) : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting("auth-register")]
    [ProducesResponseType<FreelancerRegistrationResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<FreelancerRegistrationResponse>> Register(RegisterFreelancerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Accepted(await registrationService.RegisterAsync(request, cancellationToken));
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    [HttpPost("verify-phone-otp")]
    [EnableRateLimiting("auth-otp-verify")]
    [ProducesResponseType<FreelancerPhoneVerificationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<FreelancerPhoneVerificationResponse>> VerifyPhone(VerifyFreelancerPhoneOtpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await registrationService.VerifyPhoneAsync(request, cancellationToken));
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    [HttpPost("resend-phone-otp")]
    [EnableRateLimiting("auth-otp-resend")]
    [ProducesResponseType<FreelancerRegistrationResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<FreelancerRegistrationResponse>> ResendPhoneOtp(ResendFreelancerPhoneOtpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await registrationService.ResendOtpAsync(request, cancellationToken);
            return response is null
                ? Problem(statusCode: StatusCodes.Status404NotFound, title: "OTP_CHALLENGE_NOT_FOUND", detail: "Không tìm thấy yêu cầu xác thực đang chờ.")
                : Accepted(response);
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth-login")]
    [ProducesResponseType<FreelancerLoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<FreelancerLoginResponse>> Login(FreelancerLoginRequest request, CancellationToken cancellationToken)
    {
        var response = await loginService.LoginAsync(request, cancellationToken);
        if (response is not null)
            return Ok(response);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "INVALID_CREDENTIALS",
            Detail = "Số điện thoại hoặc mật khẩu không chính xác, hoặc tài khoản chưa được kích hoạt."
        };
        problem.Extensions["code"] = "INVALID_CREDENTIALS";
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return Unauthorized(problem);
    }
}
