using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Implementations;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Abstrict.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Version1 + "/auth")]
public sealed class AuthController(ICustomerRegistrationService registrationService, ICustomerLoginService loginService) : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("auth-login")]
    [ProducesResponseType<CustomerLoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<CustomerLoginResponse>> Login(CustomerLoginRequest request, CancellationToken cancellationToken)
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

    [HttpPost("register")]
    [EnableRateLimiting("auth-register")]
    [ProducesResponseType<CustomerRegistrationResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<CustomerRegistrationResponse>> Register(
        RegisterCustomerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Accepted(await registrationService.RegisterAsync(request, cancellationToken));
        }
        catch (AuthFlowException exception)
        {
            return ToProblem(exception);
        }
    }

    [HttpPost("register-freelancer")]
    [EnableRateLimiting("auth-register")]
    [ProducesResponseType<CustomerRegistrationResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<CustomerRegistrationResponse>> RegisterFreelancer(
        RegisterCustomerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Accepted(await registrationService.RegisterAsync(request, cancellationToken, UserRole.Freelancer));
        }
        catch (AuthFlowException exception)
        {
            return ToProblem(exception);
        }
    }

    [HttpPost("verify-phone-otp")]
    [EnableRateLimiting("auth-otp-verify")]
    [ProducesResponseType<CustomerPhoneVerificationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<CustomerPhoneVerificationResponse>> VerifyPhone(
        VerifyCustomerPhoneOtpRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await registrationService.VerifyPhoneAsync(request, cancellationToken));
        }
        catch (AuthFlowException exception)
        {
            return ToProblem(exception);
        }
    }

    [HttpPost("resend-phone-otp")]
    [EnableRateLimiting("auth-otp-resend")]
    [ProducesResponseType<CustomerRegistrationResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<CustomerRegistrationResponse>> ResendPhoneOtp(
        ResendCustomerPhoneOtpRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await registrationService.ResendOtpAsync(request, cancellationToken);
            return response is null
                ? Problem(statusCode: StatusCodes.Status404NotFound, title: "OTP_CHALLENGE_NOT_FOUND", detail: "Không tìm thấy yêu cầu xác thực đang chờ.")
                : Accepted(response);
        }
        catch (AuthFlowException exception)
        {
            return ToProblem(exception);
        }
    }

    private ObjectResult ToProblem(AuthFlowException exception)
    {
        if (exception.RetryAfterSeconds is { } retryAfter)
            Response.Headers.RetryAfter = retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var problem = new ProblemDetails
        {
            Status = exception.StatusCode,
            Title = exception.Code,
            Detail = exception.Message
        };
        problem.Extensions["code"] = exception.Code;
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return StatusCode(exception.StatusCode, problem);
    }
}
