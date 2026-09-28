using System.Text;
using System.Text.Json;
using Abstrict.Api.Common;
using Abstrict.Api.Data;
using Abstrict.Api.Integrations.Identity;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Options;
using Abstrict.Api.Services.Implementations;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Abstrict.Api.Tests.Freelancer;

public sealed class FreelancerKycModelTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=abstrict;Username=test;Password=test")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void Model_ContainsFreelancerOnboardingEntities()
    {
        using var context = CreateContext();
        var model = context.Model;

        Assert.NotNull(model.FindEntityType(typeof(FreelancerApplication)));
        Assert.NotNull(model.FindEntityType(typeof(IdentityVerificationAttempt)));
        Assert.NotNull(model.FindEntityType(typeof(ApplicationSubmission)));
        Assert.NotNull(model.FindEntityType(typeof(KycConsent)));
        Assert.NotNull(model.FindEntityType(typeof(KycOperation)));
        Assert.NotNull(model.FindEntityType(typeof(IdentityClaim)));
        Assert.True(model.GetEntityTypes().Count() >= 47);
    }

    [Fact]
    public void Model_EnforcesUniqueApplicationPerUserAndProvider()
    {
        using var context = CreateContext();
        var application = context.Model.FindEntityType(typeof(FreelancerApplication))!;

        Assert.Contains(application.GetIndexes(), index =>
            index.IsUnique && index.Properties.Single().Name == nameof(FreelancerApplication.UserId));
        Assert.Contains(application.GetIndexes(), index =>
            index.IsUnique && index.Properties.Single().Name == nameof(FreelancerApplication.ProviderId));
    }

    [Fact]
    public void Model_IdentityClaimFingerprintIsUnique()
    {
        using var context = CreateContext();
        var claim = context.Model.FindEntityType(typeof(IdentityClaim))!;

        Assert.Contains(claim.GetIndexes(), index =>
            index.IsUnique && index.Properties.Single().Name == nameof(IdentityClaim.Fingerprint));
    }

    [Fact]
    public void VerificationDocument_ScanStatusDefaultsToPending()
    {
        var document = new VerificationDocument
        {
            ProviderId = Guid.NewGuid(),
            Type = VerificationDocumentType.CitizenIdFront,
            ObjectKey = "key",
            OriginalFileName = "front.jpg"
        };

        Assert.Equal(DocumentScanStatus.Pending, document.ScanStatus);
        Assert.Equal(1, document.Revision);
    }
}

public sealed class FptAiMapperTests
{
    [Fact]
    public void Map_ReturnsFailureWhenErrorCodeNonZero()
    {
        using var document = JsonDocument.Parse("""{"errorCode": 1, "errorMessage": "invalid image"}""");
        var envelope = JsonSerializer.Deserialize<FptAiEnvelope>(document.RootElement.GetRawText())!;

        var result = FptAiResponseMapper.Map(envelope, "req-1");

        Assert.False(result.Succeeded);
        Assert.Equal("FPT_1", result.ErrorCode);
        Assert.Equal("req-1", result.ProviderRequestId);
    }

    [Fact]
    public void Map_ExtractsFieldsAndPreservesLeadingZeroDocumentNumber()
    {
        const string json = """
        {
          "errorCode": 0,
          "data": [
            {
              "id": "079203001234",
              "name": "Nguyen Van A",
              "dob": "1990-05-12",
              "sex": "Nam",
              "address": "HCM",
              "issue_date": "2021-06-01",
              "issue_loc": "Cuc CS",
              "expiry_date": "2031-06-01",
              "type": "CCCD",
              "id_prob": 99.1
            }
          ]
        }
        """;
        var envelope = JsonSerializer.Deserialize<FptAiEnvelope>(json)!;

        var result = FptAiResponseMapper.Map(envelope, "req-2");

        Assert.True(result.Succeeded);
        Assert.Equal("079203001234", result.DocumentNumber);
        Assert.StartsWith("0", result.DocumentNumber);
        Assert.Equal("Nguyen Van A", result.FullName);
        Assert.Equal("2031-06-01", result.ExpiryDate);
        Assert.Contains(result.FieldConfidences, x => x.Field == "id_prob" && x.Confidence == 99.1m);
    }

    [Fact]
    public void Map_ReturnsIncompleteWhenNoData()
    {
        var envelope = JsonSerializer.Deserialize<FptAiEnvelope>("""{"errorCode": 0, "data": []}""")!;

        var result = FptAiResponseMapper.Map(envelope, null);

        Assert.False(result.Succeeded);
        Assert.Equal("OCR_INCOMPLETE", result.ErrorCode);
    }
}

public sealed class SensitiveDataTests
{
    private static IOptions<KycOptions> OptionsWithKeys(string encryptionKey = "QUJDREVGR0hJSktMTU5PUFFSU1RVVldYWVpBQkNERUY=") =>
        Microsoft.Extensions.Options.Options.Create(new KycOptions
        {
            DataProtection = new KycDataProtectionOptions
            {
                EncryptionKey = encryptionKey,
                EncryptionKeyVersion = "test-v1",
                IdentityHmacKey = "abstrict-test-identity-hmac-key-with-enough-bytes-2026"
            }
        });

    [Fact]
    public void Protector_RoundTripsAndMasks()
    {
        var protector = new SensitiveDataProtector(OptionsWithKeys());
        var encrypted = protector.Protect("123456789012");

        Assert.NotEqual("123456789012", encrypted);
        Assert.StartsWith("test-v1:", encrypted);
        Assert.Equal("123456789012", protector.Unprotect(encrypted));
        Assert.Equal("********9012", protector.Mask("123456789012"));
    }

    [Fact]
    public void Protector_ReturnsNullForTamperedPayload()
    {
        var protector = new SensitiveDataProtector(OptionsWithKeys());
        var encrypted = protector.Protect("123456789012");
        var parts = encrypted.Split(':');
        var tampered = parts[0] + ":" + parts[1][..^2] + "AA";

        Assert.Null(protector.Unprotect(tampered));
    }

    [Fact]
    public void Fingerprint_IsDeterministicAndKeyed()
    {
        var service = new IdentityFingerprintService(OptionsWithKeys());
        var other = new IdentityFingerprintService(Microsoft.Extensions.Options.Options.Create(new KycOptions
        {
            DataProtection = new KycDataProtectionOptions
            {
                IdentityHmacKey = "abstrict-test-identity-hmac-key-with-different-bytes-9"
            }
        }));

        Assert.Equal(service.Compute("079203001234"), service.Compute("079203001234"));
        Assert.NotEqual(service.Compute("079203001234"), other.Compute("079203001234"));
    }

    [Fact]
    public void Protector_ThrowsWhenKeyMissing()
    {
        var protector = new SensitiveDataProtector(Microsoft.Extensions.Options.Options.Create(new KycOptions()));

        var exception = Assert.Throws<ApiFlowException>(() => protector.Protect("secret"));
        Assert.Equal("KYC_SECURITY_NOT_CONFIGURED", exception.Code);
    }
}

public sealed class OnboardingDocumentPolicyTests
{
    private static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF, 0xE0];
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] PdfHeader = Encoding.ASCII.GetBytes("%PDF-1.7");

    [Fact]
    public void Validate_AcceptsJpegForIdentity()
    {
        OnboardingDocumentPolicy.Validate(VerificationDocumentType.CitizenIdFront, "image/jpeg", 1024, new KycUploadOptions(), JpegHeader);
    }

    [Fact]
    public void Validate_AcceptsPdfOnlyForSupportingDocuments()
    {
        OnboardingDocumentPolicy.Validate(VerificationDocumentType.CriminalRecord, "application/pdf", 1024, new KycUploadOptions(), PdfHeader);

        var exception = Assert.Throws<ApiFlowException>(() =>
            OnboardingDocumentPolicy.Validate(VerificationDocumentType.CitizenIdBack, "application/pdf", 1024, new KycUploadOptions(), PdfHeader));
        Assert.Equal("UNSUPPORTED_FILE_TYPE", exception.Code);
    }

    [Fact]
    public void Validate_RejectsFakeContentType()
    {
        var exception = Assert.Throws<ApiFlowException>(() =>
            OnboardingDocumentPolicy.Validate(VerificationDocumentType.HealthCertificate, "image/png", 1024, new KycUploadOptions(), JpegHeader));
        Assert.Equal("UNSUPPORTED_FILE_TYPE", exception.Code);
    }

    [Fact]
    public void Validate_RejectsOversizeFile()
    {
        var options = new KycUploadOptions { SelfieMaxBytes = 100 };
        var exception = Assert.Throws<ApiFlowException>(() =>
            OnboardingDocumentPolicy.Validate(VerificationDocumentType.FaceVerification, "image/png", 200, options, PngHeader));
        Assert.Equal("FILE_TOO_LARGE", exception.Code);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, exception.StatusCode);
    }

    [Fact]
    public void Validate_RejectsUnknownHeader()
    {
        var exception = Assert.Throws<ApiFlowException>(() =>
            OnboardingDocumentPolicy.Validate(VerificationDocumentType.FaceVerification, "image/jpeg", 1024, new KycUploadOptions(), [1, 2, 3, 4]));
        Assert.Equal("UNSUPPORTED_FILE_TYPE", exception.Code);
    }
}

public sealed class OnboardingCompletenessTests
{
    private static IdentityVerificationAttempt CompleteAttempt(Guid applicationId) => new()
    {
        ApplicationId = applicationId,
        OcrState = OcrState.Extracted,
        FaceState = FaceState.Matched,
        ConfirmedAtUtc = DateTimeOffset.UtcNow
    };

    private static VerificationDocument Document(Guid applicationId, VerificationDocumentType type) => new()
    {
        ProviderId = Guid.NewGuid(),
        ApplicationId = applicationId,
        Type = type,
        ObjectKey = "key",
        OriginalFileName = "file.jpg",
        SupersededAtUtc = null
    };

    [Fact]
    public void ComputeMissingFields_ReportsEveryMissingGroup()
    {
        var application = new FreelancerApplication { UserId = Guid.NewGuid(), ProviderId = Guid.NewGuid() };

        var missing = FreelancerOnboardingMapper.ComputeMissingFields(application, null, [], new KycPolicyOptions());

        Assert.Contains("legalFullName", missing);
        Assert.Contains("bankAccountNumber", missing);
        Assert.Contains("identityOcr", missing);
        Assert.Contains("faceMatch", missing);
        Assert.Contains("healthCertificate", missing);
        Assert.Contains("criminalRecord", missing);
    }

    [Fact]
    public void ComputeMissingFields_EmptyWhenEverythingPresent()
    {
        var application = new FreelancerApplication
        {
            UserId = Guid.NewGuid(),
            ProviderId = Guid.NewGuid(),
            LegalFullName = "Nguyen Van A",
            DateOfBirth = new DateOnly(1990, 5, 12),
            Gender = Gender.Male,
            PermanentAddress = "HCM",
            CurrentAddress = "HCM",
            ServiceCategoryIdsJson = JsonSerializer.Serialize(new[] { Guid.NewGuid() }),
            ServiceAreaIdsJson = JsonSerializer.Serialize(new[] { Guid.NewGuid() }),
            BankCode = "VCB",
            BankAccountNumberEncrypted = "enc"
        };
        var attempt = CompleteAttempt(application.Id);
        attempt.FrontDocumentId = Guid.NewGuid();
        attempt.BackDocumentId = Guid.NewGuid();
        var documents = new[]
        {
            Document(application.Id, VerificationDocumentType.CitizenIdFront),
            Document(application.Id, VerificationDocumentType.CitizenIdBack),
            Document(application.Id, VerificationDocumentType.HealthCertificate),
            Document(application.Id, VerificationDocumentType.CriminalRecord)
        };

        var missing = FreelancerOnboardingMapper.ComputeMissingFields(application, attempt, documents, new KycPolicyOptions());

        Assert.Empty(missing);
    }

    [Fact]
    public void ComputeMissingFields_IgnoresSupersededDocuments()
    {
        var application = new FreelancerApplication { UserId = Guid.NewGuid(), ProviderId = Guid.NewGuid() };
        var superseded = Document(application.Id, VerificationDocumentType.HealthCertificate);
        superseded.SupersededAtUtc = DateTimeOffset.UtcNow;

        var missing = FreelancerOnboardingMapper.ComputeMissingFields(application, null, [superseded], new KycPolicyOptions());

        Assert.Contains("healthCertificate", missing);
    }
}

public sealed class KycOptionsValidatorTests
{
    private static KycOptions Valid() => new()
    {
        Enabled = true,
        FptAi = new FptAiOptions { BaseUrl = "https://api.fpt.ai/", ApiKey = "key" },
        FacePlusPlus = new FacePlusPlusOptions
        {
            BaseUrl = "https://api-us.faceplusplus.com/",
            ApiKey = "key",
            ApiSecret = "secret",
            MatchThreshold = 85
        },
        DataProtection = new KycDataProtectionOptions
        {
            EncryptionKey = "QUJDREVGR0hJSktMTU5PUFFSU1RVVldYWVpBQkNERUY=",
            IdentityHmacKey = "abstrict-test-identity-hmac-key-with-enough-bytes-2026"
        }
    };

    [Fact]
    public void Validate_AcceptsCompleteConfiguration() => KycOptionsValidator.Validate(Valid());

    [Fact]
    public void Validate_RejectsMissingFaceCredentials()
    {
        var options = Valid();
        options.FacePlusPlus.ApiSecret = string.Empty;

        Assert.Throws<InvalidOperationException>(() => KycOptionsValidator.Validate(options));
    }

    [Fact]
    public void Validate_RejectsNonHttpsFptUrl()
    {
        var options = Valid();
        options.FptAi.BaseUrl = "http://api.fpt.ai/";

        Assert.Throws<InvalidOperationException>(() => KycOptionsValidator.Validate(options));
    }

    [Fact]
    public void Validate_RejectsInvalidEncryptionKey()
    {
        var options = Valid();
        options.DataProtection.EncryptionKey = "not-base64";

        Assert.Throws<InvalidOperationException>(() => KycOptionsValidator.Validate(options));
    }

    [Fact]
    public void Validate_RejectsOutOfRangeThreshold()
    {
        var options = Valid();
        options.FacePlusPlus.MatchThreshold = 150;

        Assert.Throws<InvalidOperationException>(() => KycOptionsValidator.Validate(options));
    }
}

public sealed class FreelancerRegistrationValidatorTests
{
    [Fact]
    public void Validate_NormalizesPhoneAndName()
    {
        var request = new Abstrict.Api.DTOs.Requests.RegisterFreelancerRequest
        {
            FullName = "  Tran Thi B  ",
            PhoneNumber = "+84 90 123 4567",
            Password = "abc12345",
            ConfirmPassword = "abc12345",
            AcceptTermsAndPrivacy = true
        };

        var (fullName, phoneNumber) = FreelancerRegistrationInputValidator.Validate(request);

        Assert.Equal("Tran Thi B", fullName);
        Assert.Equal("0901234567", phoneNumber);
    }

    [Fact]
    public void Validate_RequiresConsent()
    {
        var request = new Abstrict.Api.DTOs.Requests.RegisterFreelancerRequest
        {
            FullName = "Tran Thi B",
            PhoneNumber = "0901234567",
            Password = "abc12345",
            ConfirmPassword = "abc12345",
            AcceptTermsAndPrivacy = false
        };

        var exception = Assert.Throws<ApiFlowException>(() => FreelancerRegistrationInputValidator.Validate(request));
        Assert.Equal("TERMS_NOT_ACCEPTED", exception.Code);
    }
}
