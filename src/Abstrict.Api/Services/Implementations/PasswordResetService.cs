using System.Security.Cryptography;
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

/// <summary>
/// Cung cấp dịch vụ đặt lại mật khẩu bằng mã OTP SMS.
/// </summary>
public sealed class PasswordResetService(
    AppDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IPhoneOtpSender otpSender,
    OtpCodeHasher otpCodeHasher,
    PasswordResetTokenProvider tokenProvider,
    TimeProvider timeProvider,
    IHostEnvironment hostEnvironment,
    IConfiguration configuration) : IPasswordResetService
{
    private static readonly TimeSpan OtpSendWindow = TimeSpan.FromHours(24);

    public async Task<ForgotPasswordResponse> RequestResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var phoneNumber = PasswordResetInputValidator.ValidatePhoneNumber(request.PhoneNumber);
        var now = timeProvider.GetUtcNow();

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.PhoneNumber == phoneNumber, cancellationToken);
        if (user is null || user.Status != AccountStatus.Active)
            throw new AuthFlowException(StatusCodes.Status404NotFound, "USER_NOT_FOUND", "Không tìm thấy tài khoản tương ứng với số điện thoại này hoặc tài khoản chưa kích hoạt.");

        await EnsureSendLimitAsync(phoneNumber, now, cancellationToken);

        var latestChallenge = await dbContext.PhoneOtpChallenges
            .Where(x => x.UserId == user.Id && x.Purpose == OtpPurpose.PasswordReset)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestChallenge is not null && latestChallenge.ResendAvailableAtUtc > now)
            throw new AuthFlowException(StatusCodes.Status429TooManyRequests, "OTP_RESEND_COOLDOWN", "Vui lòng đợi trước khi yêu cầu mã OTP mới.",
                (int)Math.Ceiling((latestChallenge.ResendAvailableAtUtc - now).TotalSeconds));

        if (latestChallenge is not null && !latestChallenge.ConsumedAtUtc.HasValue)
            latestChallenge.ConsumedAtUtc = now;

        var challenge = CreateChallenge(user.Id, phoneNumber, now, out var code);
        dbContext.PhoneOtpChallenges.Add(challenge);
        await dbContext.SaveChangesAsync(cancellationToken);

        var devCode = await SendCodeAsync(challenge, code, cancellationToken);
        return ToResponse(challenge, devCode);
    }

    public async Task<ForgotPasswordResponse?> ResendResetOtpAsync(ResendForgotPasswordOtpRequest request, CancellationToken cancellationToken)
    {
        var sourceChallenge = await dbContext.PhoneOtpChallenges.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.ChallengeId && x.Purpose == OtpPurpose.PasswordReset, cancellationToken);
        if (sourceChallenge is null)
            return null;

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == sourceChallenge.UserId, cancellationToken);
        if (user is null || user.Status != AccountStatus.Active)
            return null;

        var now = timeProvider.GetUtcNow();
        var latestChallenge = await dbContext.PhoneOtpChallenges
            .Where(x => x.UserId == user.Id && x.Purpose == OtpPurpose.PasswordReset)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestChallenge is not null && latestChallenge.ResendAvailableAtUtc > now)
            throw new AuthFlowException(StatusCodes.Status429TooManyRequests, "OTP_RESEND_COOLDOWN", "Vui lòng đợi trước khi yêu cầu mã OTP mới.",
                (int)Math.Ceiling((latestChallenge.ResendAvailableAtUtc - now).TotalSeconds));

        await EnsureSendLimitAsync(user.PhoneNumber, now, cancellationToken);

        if (latestChallenge is not null && !latestChallenge.ConsumedAtUtc.HasValue)
            latestChallenge.ConsumedAtUtc = now;

        var challenge = CreateChallenge(user.Id, user.PhoneNumber, now, out var code);
        dbContext.PhoneOtpChallenges.Add(challenge);
        await dbContext.SaveChangesAsync(cancellationToken);

        var devCode = await SendCodeAsync(challenge, code, cancellationToken);
        return ToResponse(challenge, devCode);
    }

    public async Task<VerifyResetOtpResponse> VerifyResetOtpAsync(VerifyResetOtpRequest request, CancellationToken cancellationToken)
    {
        var challenge = await dbContext.PhoneOtpChallenges
            .SingleOrDefaultAsync(x => x.Id == request.ChallengeId && x.Purpose == OtpPurpose.PasswordReset, cancellationToken);

        if (challenge is null)
            throw new AuthFlowException(StatusCodes.Status404NotFound, "OTP_CHALLENGE_NOT_FOUND", "Không tìm thấy yêu cầu xác thực.");

        if (challenge.LockedAtUtc.HasValue)
            throw new AuthFlowException(StatusCodes.Status429TooManyRequests, "OTP_ATTEMPTS_EXCEEDED", "Bạn đã nhập sai mã OTP quá số lần cho phép. Hãy gửi mã mới.");

        if (challenge.ConsumedAtUtc.HasValue)
            throw new AuthFlowException(StatusCodes.Status409Conflict, "OTP_ALREADY_USED", "Mã OTP này đã được sử dụng. Hãy gửi mã mới.");

        var now = timeProvider.GetUtcNow();
        if (challenge.ExpiresAtUtc <= now)
        {
            challenge.ConsumedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new AuthFlowException(StatusCodes.Status410Gone, "OTP_EXPIRED", "Mã OTP đã hết hạn. Hãy yêu cầu gửi mã mới.");
        }

        if (!otpCodeHasher.Verify(request.Code, challenge.CodeHash))
        {
            var isLocked = OtpAttemptPolicy.RecordFailure(challenge, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (isLocked)
                throw new AuthFlowException(StatusCodes.Status429TooManyRequests, "OTP_ATTEMPTS_EXCEEDED", "Bạn đã nhập sai mã OTP quá số lần cho phép. Hãy gửi mã mới.");

            throw new AuthFlowException(StatusCodes.Status400BadRequest, "INVALID_OTP", "Mã OTP không chính xác.");
        }

        var resetToken = tokenProvider.GenerateResetToken(challenge.UserId, challenge.Id, now.AddMinutes(15));
        return new VerifyResetOtpResponse(resetToken, challenge.PhoneNumber);
    }

    public async Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        PasswordResetInputValidator.ValidatePassword(request.NewPassword, request.ConfirmPassword);

        var now = timeProvider.GetUtcNow();
        PhoneOtpChallenge? challenge = null;
        Guid userId = Guid.Empty;

        if (!string.IsNullOrWhiteSpace(request.ResetToken))
        {
            if (!tokenProvider.ValidateResetToken(request.ResetToken, now, out userId, out var challengeId))
                throw new AuthFlowException(StatusCodes.Status400BadRequest, "INVALID_RESET_TOKEN", "Mã xác thực đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");

            challenge = await dbContext.PhoneOtpChallenges
                .SingleOrDefaultAsync(x => x.Id == challengeId && x.UserId == userId && x.Purpose == OtpPurpose.PasswordReset, cancellationToken);

            if (challenge is null || challenge.ConsumedAtUtc.HasValue || challenge.LockedAtUtc.HasValue)
                throw new AuthFlowException(StatusCodes.Status400BadRequest, "INVALID_RESET_TOKEN", "Mã xác thực đặt lại mật khẩu không hợp lệ hoặc đã được sử dụng.");
        }
        else if (request.ChallengeId.HasValue && !string.IsNullOrWhiteSpace(request.Code))
        {
            challenge = await dbContext.PhoneOtpChallenges
                .SingleOrDefaultAsync(x => x.Id == request.ChallengeId.Value && x.Purpose == OtpPurpose.PasswordReset, cancellationToken);

            if (challenge is null)
                throw new AuthFlowException(StatusCodes.Status404NotFound, "OTP_CHALLENGE_NOT_FOUND", "Không tìm thấy yêu cầu xác thực.");

            if (challenge.LockedAtUtc.HasValue)
                throw new AuthFlowException(StatusCodes.Status429TooManyRequests, "OTP_ATTEMPTS_EXCEEDED", "Bạn đã nhập sai mã OTP quá số lần cho phép. Hãy gửi mã mới.");

            if (challenge.ConsumedAtUtc.HasValue)
                throw new AuthFlowException(StatusCodes.Status409Conflict, "OTP_ALREADY_USED", "Mã OTP này đã được sử dụng. Hãy gửi mã mới.");

            if (challenge.ExpiresAtUtc <= now)
            {
                challenge.ConsumedAtUtc = now;
                await dbContext.SaveChangesAsync(cancellationToken);
                throw new AuthFlowException(StatusCodes.Status410Gone, "OTP_EXPIRED", "Mã OTP đã hết hạn. Hãy yêu cầu gửi mã mới.");
            }

            if (!otpCodeHasher.Verify(request.Code, challenge.CodeHash))
            {
                var isLocked = OtpAttemptPolicy.RecordFailure(challenge, now);
                await dbContext.SaveChangesAsync(cancellationToken);
                if (isLocked)
                    throw new AuthFlowException(StatusCodes.Status429TooManyRequests, "OTP_ATTEMPTS_EXCEEDED", "Bạn đã nhập sai mã OTP quá số lần cho phép. Hãy gửi mã mới.");

                throw new AuthFlowException(StatusCodes.Status400BadRequest, "INVALID_OTP", "Mã OTP không chính xác.");
            }

            userId = challenge.UserId;
        }
        else
        {
            throw new AuthFlowException(StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Yêu cầu đặt lại mật khẩu không hợp lệ.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null || user.Status != AccountStatus.Active)
            throw new AuthFlowException(StatusCodes.Status404NotFound, "USER_NOT_FOUND", "Không tìm thấy tài khoản hoặc tài khoản chưa kích hoạt.");

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.UpdatedAtUtc = now;

        challenge.ConsumedAtUtc = now;
        challenge.UpdatedAtUtc = now;

        var activeSessions = await dbContext.AuthSessions
            .Where(x => x.UserId == user.Id && !x.RevokedAtUtc.HasValue)
            .ToListAsync(cancellationToken);

        foreach (var session in activeSessions)
        {
            session.RevokedAtUtc = now;
            session.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResetPasswordResponse(true, "Mật khẩu đã được đặt lại thành công. Vui lòng đăng nhập bằng mật khẩu mới.");
    }

    private async Task EnsureSendLimitAsync(string phoneNumber, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var windowStart = now - OtpSendWindow;
        var count = await dbContext.PhoneOtpChallenges.CountAsync(
            x => x.PhoneNumber == phoneNumber && x.CreatedAtUtc >= windowStart, cancellationToken);

        if (count >= CustomerRegistrationService.MaximumOtpSendsPerDay)
            throw new AuthFlowException(StatusCodes.Status429TooManyRequests, "OTP_SEND_LIMIT_EXCEEDED", "Đã vượt quá số lần gửi mã trong ngày. Vui lòng thử lại sau.");
    }

    private PhoneOtpChallenge CreateChallenge(Guid userId, string phoneNumber, DateTimeOffset now, out string code)
    {
        code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        return new PhoneOtpChallenge
        {
            UserId = userId,
            PhoneNumber = phoneNumber,
            CodeHash = Convert.ToBase64String(otpCodeHasher.Hash(code)),
            Purpose = OtpPurpose.PasswordReset,
            DeliveryChannel = OtpDeliveryChannel.Sms,
            ExpiresAtUtc = now + CustomerRegistrationService.OtpLifetime,
            ResendAvailableAtUtc = now + CustomerRegistrationService.ResendCooldown,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
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

    private ForgotPasswordResponse ToResponse(PhoneOtpChallenge challenge, string? devCode) =>
        new(challenge.Id, challenge.PhoneNumber, challenge.ExpiresAtUtc, challenge.ResendAvailableAtUtc)
        {
            DevelopmentOtpCode = OtpResponseExposurePolicy.ShouldExposeCode(
                hostEnvironment.EnvironmentName,
                configuration.GetValue<bool>("Otp:ExposeCodeToClient"),
                configuration.GetValue<bool>("Otp:UseFakeSender"))
                    ? devCode
                    : null
        };
}
