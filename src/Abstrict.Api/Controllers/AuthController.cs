using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Implementations;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Abstrict.Api.Controllers;

/// <summary>
/// Xác thực và đăng ký tài khoản khách hàng.
/// </summary>
/// <remarks>
/// Bao gồm đăng ký, xác thực số điện thoại qua OTP và đăng nhập bằng số điện thoại/mật khẩu.
/// </remarks>
[ApiController]
[Route(ApiRoutes.Version1 + "/auth")]
public sealed class AuthController(
    ICustomerRegistrationService registrationService, ICustomerLoginService loginService, IHostEnvironment environment) : ControllerBase
{
    /// <summary>
    /// Đăng nhập khách hàng.
    /// </summary>
    /// <remarks>
    /// Xác thực bằng số điện thoại và mật khẩu, trả về JWT access token khi thành công.
    /// Dùng chung cho khách hàng, freelancer, công ty và admin (<c>role</c> là Customer, Freelancer, Company hoặc Admin).
    /// Khách hàng nhận kèm <c>customerProfile</c>; freelancer nhận kèm <c>freelancerOnboarding</c> (tiến độ KYC); công ty nhận kèm <c>companyOnboarding</c>.
    /// Đồng thời đặt cookie HttpOnly <c>abstrict_refresh</c> để khôi phục phiên qua <c>POST /auth/refresh</c>.
    /// Trả về 401 nếu thông tin đăng nhập không chính xác hoặc tài khoản chưa được kích hoạt.
    /// Giới hạn tần suất theo địa chỉ IP (10 lần/phút).
    /// </remarks>
    [HttpPost("login")]
    [EnableRateLimiting("auth-login")]
    [ProducesResponseType<CustomerLoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<CustomerLoginResponse>> Login(CustomerLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await loginService.LoginAsync(request, cancellationToken);
        if (result is not null)
        {
            SetRefreshCookie(result);
            return Ok(result.Response);
        }

        return Unauthorized(AuthProblem("INVALID_CREDENTIALS",
            "Số điện thoại hoặc mật khẩu không chính xác, hoặc tài khoản chưa được kích hoạt."));
    }

    /// <summary>
    /// Khôi phục phiên đăng nhập.
    /// </summary>
    /// <remarks>
    /// Dùng cookie HttpOnly <c>abstrict_refresh</c> được đặt khi đăng nhập để cấp access token mới; không cần body.
    /// Trả về cùng cấu trúc với đăng nhập. Trả về 401 <c>SESSION_EXPIRED</c> nếu không có cookie hoặc phiên đã hết hạn/bị thu hồi.
    /// </remarks>
    [HttpPost("refresh")]
    [EnableRateLimiting("auth-refresh")]
    [ProducesResponseType<CustomerLoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<CustomerLoginResponse>> Refresh(CancellationToken cancellationToken)
    {
        var result = await loginService.RefreshAsync(Request.Cookies[RefreshCookieName], cancellationToken);
        if (result is not null)
        {
            SetRefreshCookie(result);
            return Ok(result.Response);
        }

        Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions(null));
        return Unauthorized(AuthProblem("SESSION_EXPIRED", "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."));
    }

    /// <summary>
    /// Đăng xuất.
    /// </summary>
    /// <remarks>
    /// Thu hồi phiên gắn với cookie <c>abstrict_refresh</c> và xóa cookie. Luôn trả về 204, kể cả khi không có phiên.
    /// </remarks>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await loginService.LogoutAsync(Request.Cookies[RefreshCookieName], cancellationToken);
        Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions(null));
        return NoContent();
    }

    private const string RefreshCookieName = "abstrict_refresh";

    private void SetRefreshCookie(LoginResult result) =>
        Response.Cookies.Append(RefreshCookieName, result.RefreshToken, RefreshCookieOptions(result.RefreshExpiresAtUtc));

    // Cookie chỉ gửi tới các endpoint auth; SameSite=Lax chặn POST từ site khác nên không cần CSRF token riêng.
    private CookieOptions RefreshCookieOptions(DateTimeOffset? expiresAtUtc) => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        SameSite = SameSiteMode.Lax,
        Path = "/" + ApiRoutes.Version1 + "/auth",
        Expires = expiresAtUtc
    };

    private ProblemDetails AuthProblem(string code, string detail)
    {
        var problem = new ProblemDetails { Status = StatusCodes.Status401Unauthorized, Title = code, Detail = detail };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return problem;
    }

    /// <summary>
    /// Đăng ký tài khoản khách hàng.
    /// </summary>
    /// <remarks>
    /// Tạo tài khoản khách hàng mới và gửi OTP xác thực số điện thoại.
    /// Trả về 202 kèm mã thử thách (challenge) để tiếp tục xác thực OTP.
    /// Giới hạn tần suất theo địa chỉ IP (5 lần/10 phút).
    /// </remarks>
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

    /// <summary>
    /// Đăng ký tài khoản công ty đối tác.
    /// </summary>
    /// <remarks>
    /// Cùng body và response với đăng ký khách hàng; <c>fullName</c> là họ tên người quản lý tài khoản.
    /// Xác thực OTP qua <c>/auth/verify-phone-otp</c>, sau đó đăng nhập và khai hồ sơ tại <c>/company/onboarding</c>.
    /// Giới hạn tần suất theo địa chỉ IP (5 lần/10 phút).
    /// </remarks>
    [HttpPost("register-company")]
    [EnableRateLimiting("auth-register")]
    [ProducesResponseType<CustomerRegistrationResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<CustomerRegistrationResponse>> RegisterCompany(
        RegisterCustomerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Accepted(await registrationService.RegisterAsync(request, cancellationToken, UserRole.Company));
        }
        catch (AuthFlowException exception)
        {
            return ToProblem(exception);
        }
    }

    /// <summary>
    /// Xác thực số điện thoại khách hàng bằng OTP.
    /// </summary>
    /// <remarks>
    /// Kiểm tra mã OTP của thử thách đã tạo khi đăng ký; kích hoạt tài khoản khi xác thực thành công.
    /// Trả về 410 nếu mã OTP đã hết hạn.
    /// </remarks>
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

    /// <summary>
    /// Gửi lại OTP xác thực số điện thoại khách hàng.
    /// </summary>
    /// <remarks>
    /// Gửi lại mã OTP cho thử thách còn hiệu lực; trả về 404 nếu không tìm thấy thử thách.
    /// Giới hạn tần suất theo địa chỉ IP (3 lần/10 phút).
    /// </remarks>
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
