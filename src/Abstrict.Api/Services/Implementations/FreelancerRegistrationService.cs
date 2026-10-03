using System.Security.Cryptography;
using Abstrict.Api.Common;
using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Integrations.Notifications;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Repositories.Interfaces;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Services.Implementations;

public sealed class FreelancerRegistrationService(
    AppDbContext dbContext,
    IUserRepository userRepository,
    IFreelancerApplicationRepository applicationRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher,
    IPhoneOtpSender otpSender,
    OtpCodeHasher otpCodeHasher,
    TimeProvider timeProvider,
    IHostEnvironment hostEnvironment,
    IConfiguration configuration) : IFreelancerRegistrationService
{
    public const int MaximumOtpAttempts = 5;
    public const int MaximumOtpSendsPerDay = 5;
    public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan OtpSendWindow = TimeSpan.FromHours(24);

    public async Task<FreelancerRegistrationResponse> RegisterAsync(RegisterFreelancerRequest request, CancellationToken cancellationToken)
    {
        var (fullName, phoneNumber) = FreelancerRegistrationInputValidator.Validate(request);
        var now = timeProvider.GetUtcNow();

        var existing = await userRepository.GetByPhoneNumberAsync(phoneNumber, track: false, cancellationToken);
        if (existing is not null)
            throw new ApiFlowException(StatusCodes.Status409Conflict, "PHONE_ALREADY_REGISTERED", "Số điện thoại đã được đăng ký.");

        await EnsureSendLimitAsync(phoneNumber, now, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var user = new User
        {
            PhoneNumber = phoneNumber,
            Role = UserRole.Freelancer,
            Status = AccountStatus.Pending,
            TermsAcceptedAtUtc = now,
            PrivacyPolicyAcceptedAtUtc = now
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        await userRepository.AddAsync(user, cancellationToken);

        var provider = new Provider
        {
            Type = ProviderType.Freelancer,
            DisplayName = fullName,
            ApprovalStatus = ApprovalStatus.Draft,
            IsAcceptingBookings = false
        };
        dbContext.Providers.Add(provider);

        var application = new FreelancerApplication
        {
            UserId = user.Id,
            User = user,
            ProviderId = provider.Id,
            Provider = provider,
            Version = 1,
            Status = FreelancerApplicationStatus.Draft,
            CurrentStep = OnboardingStep.Personal,
            LegalFullName = fullName
        };
        await applicationRepository.AddAsync(application, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            var challenge = CreateChallenge(user, phoneNumber, now, out var code);
            dbContext.PhoneOtpChallenges.Add(challenge);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            var developmentOtpCode = await SendCodeAsync(challenge, code, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToResponse(user, challenge, developmentOtpCode);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ApiFlowException(StatusCodes.Status409Conflict, "PHONE_ALREADY_REGISTERED", "Số điện thoại đã được đăng ký.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<FreelancerPhoneVerificationResponse> VerifyPhoneAsync(VerifyFreelancerPhoneOtpRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var challenge = await dbContext.PhoneOtpChallenges
            .FromSqlInterpolated($"SELECT * FROM phone_otp_challenges WHERE id = {request.ChallengeId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (challenge is null || challenge.Purpose != OtpPurpose.FreelancerRegistration)
            throw new ApiFlowException(StatusCodes.Status404NotFound, "OTP_CHALLENGE_NOT_FOUND", "Không tìm thấy yêu cầu xác thực.");
        if (challenge.LockedAtUtc.HasValue)
            throw new ApiFlowException(StatusCodes.Status429TooManyRequests, "OTP_ATTEMPTS_EXCEEDED", "Bạn đã nhập sai mã OTP quá số lần cho phép. Hãy gửi mã mới.");
        if (challenge.ConsumedAtUtc.HasValue)
            throw new ApiFlowException(StatusCodes.Status409Conflict, "OTP_ALREADY_USED", "Mã OTP này đã được sử dụng. Hãy gửi mã mới.");
        if (challenge.ExpiresAtUtc <= now)
        {
            challenge.ConsumedAtUtc = now;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new ApiFlowException(StatusCodes.Status410Gone, "OTP_EXPIRED", "Mã OTP đã hết hạn. Hãy yêu cầu gửi mã mới.");
        }

        if (!otpCodeHasher.Verify(request.Code, challenge.CodeHash))
        {
            var isLocked = OtpAttemptPolicy.RecordFailure(challenge, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (isLocked)
                throw new ApiFlowException(StatusCodes.Status429TooManyRequests, "OTP_ATTEMPTS_EXCEEDED", "Bạn đã nhập sai mã OTP quá số lần cho phép. Hãy gửi mã mới.");
            throw new ApiFlowException(StatusCodes.Status400BadRequest, "INVALID_OTP", "Mã OTP không chính xác.");
        }

        var user = await userRepository.GetByIdAsync(challenge.UserId, track: true, cancellationToken);
        if (user is null || user.Role != UserRole.Freelancer || user.Status != AccountStatus.Pending)
            throw new ApiFlowException(StatusCodes.Status409Conflict, "FREELANCER_REGISTRATION_NOT_PENDING", "Tài khoản không còn ở trạng thái chờ xác thực.");

        challenge.ConsumedAtUtc = now;
        user.PhoneVerifiedAtUtc = now;
        user.Status = AccountStatus.Active;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new FreelancerPhoneVerificationResponse(user.Id, "Active", now);
    }

    public async Task<FreelancerRegistrationResponse?> ResendOtpAsync(ResendFreelancerPhoneOtpRequest request, CancellationToken cancellationToken)
    {
        var sourceChallenge = await dbContext.PhoneOtpChallenges.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.ChallengeId && x.Purpose == OtpPurpose.FreelancerRegistration, cancellationToken);
        if (sourceChallenge is null)
            return null;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var user = await dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE id = {sourceChallenge.UserId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null || user.Role != UserRole.Freelancer || user.Status != AccountStatus.Pending)
            return null;

        var now = timeProvider.GetUtcNow();
        var latestChallenge = await dbContext.PhoneOtpChallenges
            .Where(x => x.UserId == user.Id && x.Purpose == OtpPurpose.FreelancerRegistration)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestChallenge is not null && latestChallenge.ResendAvailableAtUtc > now)
            throw new ApiFlowException(StatusCodes.Status429TooManyRequests, "OTP_RESEND_COOLDOWN", "Vui lòng đợi trước khi yêu cầu mã OTP mới.",
                (int)Math.Ceiling((latestChallenge.ResendAvailableAtUtc - now).TotalSeconds));

        await EnsureSendLimitAsync(user.PhoneNumber, now, cancellationToken);

        if (latestChallenge is not null && !latestChallenge.ConsumedAtUtc.HasValue)
            latestChallenge.ConsumedAtUtc = now;

        var challenge = CreateChallenge(user, user.PhoneNumber, now, out var code);
        dbContext.PhoneOtpChallenges.Add(challenge);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var developmentOtpCode = await SendCodeAsync(challenge, code, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(user, challenge, developmentOtpCode);
    }

    private async Task EnsureSendLimitAsync(string phoneNumber, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var windowStart = now - OtpSendWindow;
        var sends = await dbContext.PhoneOtpChallenges.CountAsync(
            x => x.PhoneNumber == phoneNumber && x.Purpose == OtpPurpose.FreelancerRegistration && x.CreatedAtUtc >= windowStart,
            cancellationToken);
        if (sends >= MaximumOtpSendsPerDay)
            throw new ApiFlowException(StatusCodes.Status429TooManyRequests, "OTP_SEND_LIMIT_EXCEEDED", "Đã vượt quá số lần gửi mã trong ngày. Vui lòng thử lại sau.");
    }

    private async Task<string?> SendCodeAsync(PhoneOtpChallenge challenge, string code, CancellationToken cancellationToken)
    {
        try
        {
            return await otpSender.SendAsync(challenge.PhoneNumber, code, challenge.DeliveryChannel, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new ApiFlowException(StatusCodes.Status503ServiceUnavailable, "OTP_DELIVERY_UNAVAILABLE", "Dịch vụ gửi mã xác thực hiện chưa được cấu hình.", innerException: exception);
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
            Purpose = OtpPurpose.FreelancerRegistration,
            DeliveryChannel = OtpDeliveryChannel.Sms,
            ExpiresAtUtc = now + OtpLifetime,
            ResendAvailableAtUtc = now + ResendCooldown,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private FreelancerRegistrationResponse ToResponse(User user, PhoneOtpChallenge challenge, string? developmentOtpCode) =>
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

public static class FreelancerRegistrationInputValidator
{
    public static (string FullName, string PhoneNumber) Validate(RegisterFreelancerRequest request)
    {
        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            throw new ApiFlowException(StatusCodes.Status400BadRequest, "PASSWORD_CONFIRMATION_MISMATCH", "Mật khẩu xác nhận không khớp.");
        if (!request.AcceptTermsAndPrivacy)
            throw new ApiFlowException(StatusCodes.Status400BadRequest, "TERMS_NOT_ACCEPTED", "Bạn cần đồng ý với điều khoản dịch vụ và chính sách quyền riêng tư.");

        var fullName = request.FullName.Trim();
        if (fullName.Length < 2)
            throw new ApiFlowException(StatusCodes.Status400BadRequest, "INVALID_FULL_NAME", "Họ và tên phải có ít nhất 2 ký tự.");

        var phoneNumber = VietnamesePhoneNumber.Normalize(request.PhoneNumber);
        if (phoneNumber is null)
            throw new ApiFlowException(StatusCodes.Status400BadRequest, "INVALID_PHONE_NUMBER", "Số điện thoại Việt Nam phải có 10 hoặc 11 chữ số.");

        return (fullName, phoneNumber);
    }
}
