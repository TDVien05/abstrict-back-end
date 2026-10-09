using Abstrict.Api.Data;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Abstrict.Api.Tests.Architecture;

public sealed class ProjectStructureTests
{
    [Fact]
    public void ApiAssembly_CanBeLoaded()
    {
        var assembly = typeof(Program).Assembly;

        Assert.Equal("Abstrict.Api", assembly.GetName().Name);
    }

    [Fact]
    public void EfCoreModel_ContainsCoreScreenEntities()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=abstrict;Username=test;Password=test")
            .Options;

        using var context = new AppDbContext(options);
        var model = context.Model;

        Assert.NotNull(model.FindEntityType(typeof(Provider)));
        Assert.NotNull(model.FindEntityType(typeof(Booking)));
        Assert.NotNull(model.FindEntityType(typeof(Payment)));
        Assert.NotNull(model.FindEntityType(typeof(PromotionPurchase)));
        Assert.NotNull(model.FindEntityType(typeof(Dispute)));
        Assert.NotNull(model.FindEntityType(typeof(ProviderApprovalReview)));
        Assert.NotNull(model.FindEntityType(typeof(ProviderAssessment)));
        Assert.NotNull(model.FindEntityType(typeof(ProviderWallet)));
        Assert.NotNull(model.FindEntityType(typeof(DisputeMessage)));
        Assert.NotNull(model.FindEntityType(typeof(AuditLog)));
        Assert.NotNull(model.FindEntityType(typeof(PhoneOtpChallenge)));
        Assert.NotNull(model.FindEntityType(typeof(AuthSession)));
        Assert.True(model.GetEntityTypes().Count() >= 41);
    }

    [Fact]
    public void CustomerAuthenticationModel_MatchesStitchScreens()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=abstrict;Username=test;Password=test")
            .Options;

        using var context = new AppDbContext(options);
        var user = context.Model.FindEntityType(typeof(User))!;
        var otpChallenge = context.Model.FindEntityType(typeof(PhoneOtpChallenge))!;
        var authSession = context.Model.FindEntityType(typeof(AuthSession))!;

        Assert.True(user.FindProperty(nameof(User.Email))!.IsNullable);
        Assert.False(user.FindProperty(nameof(User.PhoneNumber))!.IsNullable);
        Assert.Equal(500, otpChallenge.FindProperty(nameof(PhoneOtpChallenge.CodeHash))!.GetMaxLength());
        Assert.Equal(500, authSession.FindProperty(nameof(AuthSession.RefreshTokenHash))!.GetMaxLength());
        Assert.Contains(authSession.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name == nameof(AuthSession.RefreshTokenHash));
    }
}

public sealed class CustomerRegistrationPolicyTests
{
    [Theory]
    [InlineData("090 123 4567", "0901234567")]
    [InlineData("+84 90 123 4567", "0901234567")]
    [InlineData("84901234567", "0901234567")]
    [InlineData("abc0901234567", null)]
    [InlineData("1234567890", null)]
    public void VietnamesePhoneNumber_NormalizesOnlyValidDomesticNumbers(string input, string? expected) =>
        Assert.Equal(expected, VietnamesePhoneNumber.Normalize(input));

    [Fact]
    public void RegisterRequest_RequiresNamePhoneStrongPasswordAndConsent()
    {
        var request = new RegisterCustomerRequest
        {
            FullName = "Nguyễn Thu Hà",
            PhoneNumber = "0901234567",
            Password = "abc12345",
            ConfirmPassword = "abc12345",
            AcceptTermsAndPrivacy = true
        };

        Assert.Empty(Validate(request));

        var weakPasswordRequest = new RegisterCustomerRequest
        {
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Password = "12345678",
            ConfirmPassword = "12345678",
            AcceptTermsAndPrivacy = true
        };
        Assert.Contains(Validate(weakPasswordRequest), error => error.MemberNames.Contains(nameof(request.Password)));

        var missingInputRequest = new RegisterCustomerRequest();
        Assert.NotEmpty(Validate(missingInputRequest));
    }

    [Fact]
    public void OtpHasher_StoresKeyedVerifierAndChecksIt()
    {
        var hasher = new OtpCodeHasher("test-key-with-at-least-32-bytes-long");
        var storedHash = Convert.ToBase64String(hasher.Hash("123456"));

        Assert.DoesNotContain("123456", storedHash);
        Assert.True(hasher.Verify("123456", storedHash));
        Assert.False(hasher.Verify("123457", storedHash));
    }

    [Fact]
    public void OtpAttemptPolicy_LocksChallengeOnFifthFailure()
    {
        var challenge = new PhoneOtpChallenge
        {
            UserId = Guid.NewGuid(),
            PhoneNumber = "0901234567",
            CodeHash = "protected",
            Purpose = Abstrict.Api.Models.Enums.OtpPurpose.CustomerRegistration,
            DeliveryChannel = Abstrict.Api.Models.Enums.OtpDeliveryChannel.Sms,
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

    [Fact]
    public void RegistrationInputValidator_RequiresPasswordConfirmationAndConsent()
    {
        var request = new RegisterCustomerRequest
        {
            FullName = " Nguyễn Thu Hà ",
            PhoneNumber = "090 123 4567",
            Password = "abc12345",
            ConfirmPassword = "different123",
            AcceptTermsAndPrivacy = true
        };

        var mismatch = Assert.Throws<AuthFlowException>(() => CustomerRegistrationInputValidator.Validate(request));
        Assert.Equal("PASSWORD_CONFIRMATION_MISMATCH", mismatch.Code);

        request = new RegisterCustomerRequest
        {
            FullName = " Nguyễn Thu Hà ",
            PhoneNumber = "090 123 4567",
            Password = "abc12345",
            ConfirmPassword = "abc12345",
            AcceptTermsAndPrivacy = false
        };
        var noConsent = Assert.Throws<AuthFlowException>(() => CustomerRegistrationInputValidator.Validate(request));
        Assert.Equal("TERMS_NOT_ACCEPTED", noConsent.Code);

        request = new RegisterCustomerRequest
        {
            FullName = " Nguyễn Thu Hà ",
            PhoneNumber = "090 123 4567",
            Password = "abc12345",
            ConfirmPassword = "abc12345",
            AcceptTermsAndPrivacy = true
        };
        Assert.Equal(new NormalizedCustomerRegistration("Nguyễn Thu Hà", "0901234567"), CustomerRegistrationInputValidator.Validate(request));
    }

    [Fact]
    public void RegistrationResponse_OmitsDevelopmentCodeUnlessPresent()
    {
        var response = new CustomerRegistrationResponse(
            Guid.NewGuid(), Guid.NewGuid(), "0901234567", DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddSeconds(45));
        var productionJson = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("developmentOtpCode", productionJson);

        var developmentJson = JsonSerializer.Serialize(response with { DevelopmentOtpCode = "012345" }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"developmentOtpCode\":\"012345\"", developmentJson);
    }

    [Theory]
    [InlineData("Development", true, false, true)]
    [InlineData("Development", false, false, false)]
    [InlineData("Production", true, false, false)]
    [InlineData("Production", false, true, false)]
    [InlineData("Production", true, true, true)]
    [InlineData("Staging", true, false, false)]
    public void OtpResponseExposurePolicy_RequiresExplicitSettingAndFakeSenderOutsideDevelopment(
        string environmentName,
        bool configured,
        bool useFakeSender,
        bool expected) =>
        Assert.Equal(expected, OtpResponseExposurePolicy.ShouldExposeCode(environmentName, configured, useFakeSender));

    private static IList<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        return results;
    }
}
