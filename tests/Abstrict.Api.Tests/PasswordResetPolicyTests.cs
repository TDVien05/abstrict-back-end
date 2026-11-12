using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Implementations;

namespace Abstrict.Api.Tests;

public sealed class PasswordResetPolicyTests
{
    private const string TestSecretKey = "abstrict-test-super-secret-key-with-at-least-32-bytes!";

    [Theory]
    [InlineData("090 123 4567", "0901234567")]
    [InlineData("+84 90 123 4567", "0901234567")]
    [InlineData("84901234567", "0901234567")]
    [InlineData("0987654321", "0987654321")]
    public void ValidatePhoneNumber_NormalizesValidVietnameseNumbers(string input, string expected)
    {
        var normalized = PasswordResetInputValidator.ValidatePhoneNumber(input);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc0901234567")]
    [InlineData("1234567890")]
    [InlineData(null)]
    public void ValidatePhoneNumber_ThrowsForInvalidNumbers(string? input)
    {
        var exception = Assert.Throws<AuthFlowException>(() => PasswordResetInputValidator.ValidatePhoneNumber(input));
        Assert.Equal("INVALID_PHONE_NUMBER", exception.Code);
    }

    [Fact]
    public void ValidatePassword_AcceptsStrongPasswordMatchingConfirmation()
    {
        var exception = Record.Exception(() => PasswordResetInputValidator.ValidatePassword("SecurePass123", "SecurePass123"));
        Assert.Null(exception);
    }

    [Fact]
    public void ValidatePassword_ThrowsWhenConfirmationMismatches()
    {
        var exception = Assert.Throws<AuthFlowException>(() => PasswordResetInputValidator.ValidatePassword("SecurePass123", "Different123"));
        Assert.Equal("PASSWORD_CONFIRMATION_MISMATCH", exception.Code);
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("onlyletters")]
    [InlineData("1234567890")]
    [InlineData("")]
    [InlineData(null)]
    public void ValidatePassword_ThrowsForWeakPassword(string? password)
    {
        var exception = Assert.Throws<AuthFlowException>(() => PasswordResetInputValidator.ValidatePassword(password, password));
        Assert.Equal("WEAK_PASSWORD", exception.Code);
    }

    [Fact]
    public void TokenProvider_GeneratesAndValidatesResetToken()
    {
        var provider = new PasswordResetTokenProvider(TestSecretKey);
        var userId = Guid.NewGuid();
        var challengeId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(15);

        var token = provider.GenerateResetToken(userId, challengeId, expiresAt);

        Assert.NotNull(token);
        Assert.Contains("PR1.", token);

        var isValid = provider.ValidateResetToken(token, now, out var parsedUser, out var parsedChallenge);

        Assert.True(isValid);
        Assert.Equal(userId, parsedUser);
        Assert.Equal(challengeId, parsedChallenge);
    }

    [Fact]
    public void TokenProvider_RejectsExpiredToken()
    {
        var provider = new PasswordResetTokenProvider(TestSecretKey);
        var userId = Guid.NewGuid();
        var challengeId = Guid.NewGuid();
        var past = DateTimeOffset.UtcNow.AddMinutes(-5);

        var token = provider.GenerateResetToken(userId, challengeId, past);

        var isValid = provider.ValidateResetToken(token, DateTimeOffset.UtcNow, out _, out _);
        Assert.False(isValid);
    }

    [Fact]
    public void TokenProvider_RejectsTamperedToken()
    {
        var provider = new PasswordResetTokenProvider(TestSecretKey);
        var userId = Guid.NewGuid();
        var challengeId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

        var token = provider.GenerateResetToken(userId, challengeId, expiresAt);
        var tampered = token.Replace("PR1", "PR2");

        var isValid = provider.ValidateResetToken(tampered, DateTimeOffset.UtcNow, out _, out _);
        Assert.False(isValid);

        // Tamper signature
        var tamperedSignature = token[..^4] + "AAAA";
        Assert.False(provider.ValidateResetToken(tamperedSignature, DateTimeOffset.UtcNow, out _, out _));
    }

    [Fact]
    public void TokenProvider_RejectsInvalidKeyOrFormat()
    {
        var provider1 = new PasswordResetTokenProvider(TestSecretKey);
        var provider2 = new PasswordResetTokenProvider("a-completely-different-secret-key-with-32-bytes!");

        var token = provider1.GenerateResetToken(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(10));

        Assert.False(provider2.ValidateResetToken(token, DateTimeOffset.UtcNow, out _, out _));
        Assert.False(provider1.ValidateResetToken(null, DateTimeOffset.UtcNow, out _, out _));
        Assert.False(provider1.ValidateResetToken("invalid.token", DateTimeOffset.UtcNow, out _, out _));
    }

    [Fact]
    public void ForgotPasswordResponse_OmitsDevelopmentCodeUnlessPresent()
    {
        var challengeId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var response = new ForgotPasswordResponse(challengeId, "0901234567", now.AddMinutes(5), now.AddSeconds(45));

        var jsonWithout = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("developmentOtpCode", jsonWithout);

        var withDevCode = response with { DevelopmentOtpCode = "123456" };
        var jsonWith = JsonSerializer.Serialize(withDevCode, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"developmentOtpCode\":\"123456\"", jsonWith);
    }

    [Fact]
    public void VerifyResetOtpRequest_ValidatesCodeFormat()
    {
        var validRequest = new VerifyResetOtpRequest
        {
            ChallengeId = Guid.NewGuid(),
            Code = "123456"
        };
        Assert.Empty(Validate(validRequest));

        var invalidRequest = new VerifyResetOtpRequest
        {
            ChallengeId = Guid.NewGuid(),
            Code = "12a45"
        };
        var errors = Validate(invalidRequest);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(VerifyResetOtpRequest.Code)));
    }

    [Fact]
    public void ResetPasswordRequest_RequiresPasswordAndConfirmation()
    {
        var valid = new ResetPasswordRequest
        {
            ResetToken = "token",
            NewPassword = "NewPassword123",
            ConfirmPassword = "NewPassword123"
        };
        Assert.Empty(Validate(valid));

        var invalid = new ResetPasswordRequest
        {
            NewPassword = "short",
            ConfirmPassword = "short"
        };
        var errors = Validate(invalid);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(ResetPasswordRequest.NewPassword)));
    }

    [Fact]
    public void OtpAttemptPolicy_LocksPasswordResetChallengeOnFifthFailure()
    {
        var challenge = new PhoneOtpChallenge
        {
            UserId = Guid.NewGuid(),
            PhoneNumber = "0901234567",
            CodeHash = "hash",
            Purpose = OtpPurpose.PasswordReset,
            DeliveryChannel = OtpDeliveryChannel.Sms,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5),
            ResendAvailableAtUtc = DateTimeOffset.UtcNow.AddSeconds(45)
        };
        var now = DateTimeOffset.UtcNow;

        for (var attempt = 1; attempt < CustomerRegistrationService.MaximumOtpAttempts; attempt++)
            Assert.False(OtpAttemptPolicy.RecordFailure(challenge, now));

        Assert.True(OtpAttemptPolicy.RecordFailure(challenge, now));
        Assert.Equal(CustomerRegistrationService.MaximumOtpAttempts, challenge.FailedAttemptCount);
        Assert.Equal(now, challenge.LockedAtUtc);
        Assert.Equal(now, challenge.ConsumedAtUtc);
    }

    private static IList<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        return results;
    }
}
