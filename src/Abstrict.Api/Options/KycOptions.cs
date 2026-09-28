namespace Abstrict.Api.Options;

public sealed class KycOptions
{
    public const string SectionName = "Kyc";

    public bool Enabled { get; set; }
    public FptAiOptions FptAi { get; set; } = new();
    public FacePlusPlusOptions FacePlusPlus { get; set; } = new();
    public KycUploadOptions Upload { get; set; } = new();
    public KycPolicyOptions Policy { get; set; } = new();
    public KycDataProtectionOptions DataProtection { get; set; } = new();
    public KycStorageOptions Storage { get; set; } = new();
    public List<BankOption> Banks { get; set; } = [];
}

public sealed class BankOption
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class FptAiOptions
{
    public string BaseUrl { get; set; } = "https://api.fpt.ai/";
    public string IdentityPath { get; set; } = "vision/idr/vnm/";
    public string ApiKeyHeaderName { get; set; } = "api_key";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class FacePlusPlusOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string DetectPath { get; set; } = "facepp/v3/detect";
    public string ComparePath { get; set; } = "facepp/v3/compare";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public decimal? MatchThreshold { get; set; }
}

public sealed class KycUploadOptions
{
    public long IdentityMaxBytes { get; set; } = 5_000_000;
    public long SelfieMaxBytes { get; set; } = 5_000_000;
    public long SupportingDocumentMaxBytes { get; set; } = 15_000_000;
}

public sealed class KycPolicyOptions
{
    public string Version { get; set; } = "draft-v1";
    public bool RequireHealthCertificate { get; set; } = true;
    public bool RequireCriminalRecord { get; set; } = true;
    public bool AllowManualReview { get; set; } = true;
    public int MaxFaceAttemptsPerDay { get; set; } = 5;
    public string LivenessMode { get; set; } = "NotImplemented";
}

public sealed class KycDataProtectionOptions
{
    public string EncryptionKey { get; set; } = string.Empty;
    public string EncryptionKeyVersion { get; set; } = string.Empty;
    public string IdentityHmacKey { get; set; } = string.Empty;
    public int? RetentionDays { get; set; }
}

public sealed class KycStorageOptions
{
    public string Provider { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public int SignedUrlTtlSeconds { get; set; } = 60;
    public string LocalRootPath { get; set; } = string.Empty;
}
