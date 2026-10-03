using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Integrations.Notifications;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Services.Implementations;

public sealed class CustomerRegistrationService(
    AppDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IPhoneOtpSender otpSender,
    OtpCodeHasher otpCodeHasher,
    TimeProvider timeProvider,
    IHostEnvironment hostEnvironment,
    IConfiguration configuration) : ICustomerRegistrationService
{
    public const int OtpLength = 6;
    public const int MaximumOtpAttempts = 5;
    public const int MaximumOtpSendsPerDay = 5;
    public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan OtpSendWindow = TimeSpan.FromHours(24);

    public async Task<CustomerRegistrationResponse> RegisterAsync(RegisterCustomerRequest request, CancellationToken cancellationToken)
    {
        var normalizedRequest = CustomerRegistrationInputValidator.Validate(request);
        var fullName = normalizedRequest.FullName;
        var phoneNumber = normalizedRequest.PhoneNumber;

        var now = timeProvider.GetUtcNow();
        if (await dbContext.Users.AnyAsync(x => x.PhoneNumber == phoneNumber, cancellationToken))
            throw new AuthFlowException(StatusCodes.Status409Conflict, "PHONE_NUMBER_ALREADY_REGISTERED", "Số điện thoại đã được đăng ký.");

        await EnsureSendLimitAsync(phoneNumber, now, cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = new User
        {
            Email = null,
            PhoneNumber = phoneNumber,
            Role = UserRole.Customer,
            Status = AccountStatus.Pending
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        var profile = new CustomerProfile { User = user, FullName = fullName };
        user.CustomerProfile = profile;
        var acceptedAtUtc = now;
        user.TermsAcceptedAtUtc = acceptedAtUtc;
        user.PrivacyPolicyAcceptedAtUtc = acceptedAtUtc;
        dbContext.Users.Add(user);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            var challenge = CreateChallenge(user, phoneNumber, now, out var code);
            dbContext.PhoneOtpChallenges.Add(challenge);
            await dbContext.SaveChangesAsync(cancellationToken);
            var developmentOtpCode = await SendCodeAsync(challenge, code, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToResponse(user, challenge, developmentOtpCode);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new AuthFlowException(StatusCodes.Status409Conflict, "PHONE_NUMBER_ALREADY_REGISTERED", "Số điện thoại đã được đăng ký.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<CustomerPhoneVerificationResponse> VerifyPhoneAsync(VerifyCustomerPhoneOtpRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var challenge = await dbContext.PhoneOtpChallenges
            .FromSqlInterpolated($"SELECT * FROM phone_otp_challenges WHERE id = {request.ChallengeId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (challenge is null || challenge.Purpose != OtpPurpose.CustomerRegistration)
            throw new AuthFlowException(StatusCodes.Status404NotFound, "OTP_CHALLENGE_NOT_FOUND", "Không tìm thấy yêu cầu xác thực.");
        if (challenge.LockedAtUtc.HasValue)
            throw new AuthFlowException(StatusCodes.Status429TooManyRequests, "OTP_ATTEMPTS_EXCEEDED", "Bạn đã nhập sai mã OTP quá số lần cho phép. Hãy gửi mã mới.");
        if (challenge.ConsumedAtUtc.HasValue)
            throw new AuthFlowException(StatusCodes.Status409Conflict, "OTP_ALREADY_USED", "Mã OTP này đã được sử dụng. Hãy gửi mã mới.");
        if (challenge.ExpiresAtUtc <= now)
        {
            challenge.ConsumedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new AuthFlowException(StatusCodes.Status410Gone, "OTP_EXPIRED", "Mã OTP đã hết hạn. Hãy yêu cầu gửi mã mới.");
        }

        if (!otpCodeHasher.Verify(request.Code, challenge.CodeHash))
        {
            var isLocked = OtpAttemptPolicy.RecordFailure(challenge, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (isLocked)
                throw new AuthFlowException(StatusCodes.Status429TooManyRequests, "OTP_ATTEMPTS_EXCEEDED", "Bạn đã nhập sai mã OTP quá số lần cho phép. Hãy gửi mã mới.");
            throw new AuthFlowException(StatusCodes.Status400BadRequest, "INVALID_OTP", "Mã OTP không chính xác.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == challenge.UserId, cancellationToken);
        if (user is null || user.Role != UserRole.Customer || user.Status != AccountStatus.Pending)
            throw new AuthFlowException(StatusCodes.Status409Conflict, "CUSTOMER_REGISTRATION_NOT_PENDING", "Tài khoản không còn ở trạng thái chờ xác thực.");

        challenge.ConsumedAtUtc = now;
        user.PhoneVerifiedAtUtc = now;
        user.Status = AccountStatus.Active;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CustomerPhoneVerificationResponse(user.Id, "Active", now);
    }

    public async Task<CustomerRegistrationResponse?> ResendOtpAsync(ResendCustomerPhoneOtpRequest request, CancellationToken cancellationToken)
    {
        var sourceChallenge = await dbContext.PhoneOtpChallenges.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.ChallengeId && x.Purpose == OtpPurpose.CustomerRegistration, cancellationToken);
        if (sourceChallenge is null)
            return null;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE id = {sourceChallenge.UserId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null || user.Role != UserRole.Customer || user.Status != AccountStatus.Pending)
            return null;

        var now = timeProvider.GetUtcNow();
        var latestChallenge = await dbContext.PhoneOtpChallenges
            .Where(x => x.UserId == user.Id && x.Purpose == OtpPurpose.CustomerRegistration)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestChallenge is not null && latestChallenge.ResendAvailableAtUtc > now)
            throw new AuthFlowException(StatusCodes.Status429TooManyRequests, "OTP_RESEND_COOLDOWN", "Vui lòng đợi trước khi yêu cầu mã OTP mới.",
                (int)Math.Ceiling((latestChallenge.ResendAvailableAtUtc - now).TotalSeconds));

        await EnsureSendLimitAsync(user.PhoneNumber, now, cancellationToken);

        if (latestChallenge is not null && !latestChallenge.ConsumedAtUtc.HasValue)
            latestChallenge.ConsumedAtUtc = now;

        var challenge = CreateChallenge(user, user.PhoneNumber, now, out var code);
        dbContext.PhoneOtpChallenges.Add(challenge);
        await dbContext.SaveChangesAsync(cancellationToken);
        var developmentOtpCode = await SendCodeAsync(challenge, code, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(user, challenge, developmentOtpCode);
    }

    private async Task EnsureSendLimitAsync(string phoneNumber, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var windowStart = now - OtpSendWindow;
        var sends = await dbContext.PhoneOtpChallenges.CountAsync(
            x => x.PhoneNumber == phoneNumber && x.CreatedAtUtc >= windowStart, cancellationToken);
        if (sends >= MaximumOtpSendsPerDay)
            throw new AuthFlowException(StatusCodes.Status429TooManyRequests, "OTP_SEND_LIMIT_EXCEEDED", "Đã vượt quá số lần gửi mã trong ngày. Vui lòng thử lại sau.");
    }

    private async Task<string?> SendCodeAsync(PhoneOtpChallenge challenge, string code, CancellationToken cancellationToken)
    {
        try
        {
            return await otpSender.SendAsync(challenge.PhoneNumber, code, challenge.DeliveryChannel, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new AuthFlowException(StatusCodes.Status503ServiceUnavailable, "OTP_DELIVERY_UNAVAILABLE", "Dịch vụ gửi mã xác thực hiện chưa được cấu hình.", innerException: exception);
        }
    }

    private PhoneOtpChallenge CreateChallenge(User user, string phoneNumber, DateTimeOffset now, out string code)
    {
        code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        return new PhoneOtpChallenge
        {
            UserId = user.Id,
            PhoneNumber = phoneNumber,
            CodeHash = Convert.ToBase64String(otpCodeHasher.Hash(code)),
            Purpose = OtpPurpose.CustomerRegistration,
            DeliveryChannel = OtpDeliveryChannel.Sms,
            ExpiresAtUtc = now + OtpLifetime,
            ResendAvailableAtUtc = now + ResendCooldown,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private CustomerRegistrationResponse ToResponse(User user, PhoneOtpChallenge challenge, string? developmentOtpCode) =>
        new(user.Id, challenge.Id, challenge.PhoneNumber, challenge.ExpiresAtUtc, challenge.ResendAvailableAtUtc)
        {
            DevelopmentOtpCode = OtpResponseExposurePolicy.ShouldExposeCode(
                hostEnvironment.EnvironmentName,
                configuration.GetValue<bool>("Otp:ExposeCodeToClient"),
                configuration.GetValue<bool>("Otp:UseFakeSender"))
                    ? developmentOtpCode
                    : null
        };
}

public static class VietnamesePhoneNumber
{
    public static string? Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || !Regex.IsMatch(input, @"^\+?[0-9\s()\-]+$"))
            return null;

        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (input.TrimStart().StartsWith('+') && digits.StartsWith("84", StringComparison.Ordinal))
            digits = "0" + digits[2..];
        else if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length is 11 or 12)
            digits = "0" + digits[2..];

        return digits.Length is 10 or 11 && digits[0] == '0' ? digits : null;
    }
}

public sealed record NormalizedCustomerRegistration(string FullName, string PhoneNumber);

public static class CustomerRegistrationInputValidator
{
    public static NormalizedCustomerRegistration Validate(RegisterCustomerRequest request)
    {
        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            throw new AuthFlowException(StatusCodes.Status400BadRequest, "PASSWORD_CONFIRMATION_MISMATCH", "Mật khẩu xác nhận không khớp.");
        if (!request.AcceptTermsAndPrivacy)
            throw new AuthFlowException(StatusCodes.Status400BadRequest, "TERMS_NOT_ACCEPTED", "Bạn cần đồng ý với điều khoản dịch vụ và chính sách quyền riêng tư.");

        var fullName = request.FullName.Trim();
        if (fullName.Length < 2)
            throw new AuthFlowException(StatusCodes.Status400BadRequest, "INVALID_FULL_NAME", "Họ và tên phải có ít nhất 2 ký tự.");

        var phoneNumber = VietnamesePhoneNumber.Normalize(request.PhoneNumber);
        if (phoneNumber is null)
            throw new AuthFlowException(StatusCodes.Status400BadRequest, "INVALID_PHONE_NUMBER", "Số điện thoại Việt Nam phải có 10 hoặc 11 chữ số.");

        return new NormalizedCustomerRegistration(fullName, phoneNumber);
    }
}

public sealed class AuthFlowException(
    int statusCode,
    string code,
    string message,
    int? retryAfterSeconds = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public int? RetryAfterSeconds { get; } = retryAfterSeconds;
}
