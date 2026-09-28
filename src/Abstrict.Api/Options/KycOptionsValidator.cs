using System.Text;

namespace Abstrict.Api.Options;

public static class KycOptionsValidator
{
    public static void Validate(KycOptions options)
    {
        var fpt = options.FptAi;
        if (string.IsNullOrWhiteSpace(fpt.BaseUrl) || !Uri.TryCreate(fpt.BaseUrl, UriKind.Absolute, out var fptUri) || fptUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Kyc:FptAi:BaseUrl must be a valid HTTPS URL when Kyc:Enabled is true.");
        if (string.IsNullOrWhiteSpace(fpt.ApiKey))
            throw new InvalidOperationException("Kyc:FptAi:ApiKey must be configured when Kyc:Enabled is true.");
        if (fpt.TimeoutSeconds <= 0)
            throw new InvalidOperationException("Kyc:FptAi:TimeoutSeconds must be positive.");

        var face = options.FacePlusPlus;
        if (string.IsNullOrWhiteSpace(face.BaseUrl) || !Uri.TryCreate(face.BaseUrl, UriKind.Absolute, out var faceUri) || faceUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Kyc:FacePlusPlus:BaseUrl must be a valid HTTPS URL when Kyc:Enabled is true.");
        if (string.IsNullOrWhiteSpace(face.ApiKey) || string.IsNullOrWhiteSpace(face.ApiSecret))
            throw new InvalidOperationException("Kyc:FacePlusPlus credentials must be configured when Kyc:Enabled is true.");
        if (face.TimeoutSeconds <= 0)
            throw new InvalidOperationException("Kyc:FacePlusPlus:TimeoutSeconds must be positive.");
        if (face.MatchThreshold is { } threshold && (threshold < 0 || threshold > 100))
            throw new InvalidOperationException("Kyc:FacePlusPlus:MatchThreshold must be between 0 and 100.");

        ValidateEncryptionKey(options.DataProtection.EncryptionKey);
        var hmacKey = options.DataProtection.IdentityHmacKey;
        if (string.IsNullOrWhiteSpace(hmacKey) || Encoding.UTF8.GetByteCount(hmacKey) < 32)
            throw new InvalidOperationException("Kyc:DataProtection:IdentityHmacKey must contain at least 32 UTF-8 bytes when Kyc:Enabled is true.");

        if (options.Upload.IdentityMaxBytes <= 0 || options.Upload.SelfieMaxBytes <= 0 || options.Upload.SupportingDocumentMaxBytes <= 0)
            throw new InvalidOperationException("Kyc:Upload limits must be positive.");
    }

    private static void ValidateEncryptionKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Kyc:DataProtection:EncryptionKey must be configured when Kyc:Enabled is true.");
        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(key);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Kyc:DataProtection:EncryptionKey must be Base64 encoded.");
        }

        if (decoded.Length is not (16 or 24 or 32))
            throw new InvalidOperationException("Kyc:DataProtection:EncryptionKey must decode to 16, 24 or 32 bytes.");
    }
}
