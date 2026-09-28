using Abstrict.Api.Common;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Options;
using Microsoft.Extensions.Options;

namespace Abstrict.Api.Services.Implementations;

public static class OnboardingDocumentPolicy
{
    private static readonly string[] ImageContentTypes = ["image/jpeg", "image/png"];
    private static readonly string[] DocumentContentTypes = ["image/jpeg", "image/png", "application/pdf"];

    public static bool IsIdentityType(VerificationDocumentType type) =>
        type is VerificationDocumentType.CitizenIdFront or VerificationDocumentType.CitizenIdBack or VerificationDocumentType.FaceVerification;

    public static bool IsSupportingType(VerificationDocumentType type) =>
        type is VerificationDocumentType.HealthCertificate or VerificationDocumentType.CriminalRecord;

    public static void Validate(VerificationDocumentType type, string? contentType, long byteSize, KycUploadOptions upload, byte[] content)
    {
        if (!IsIdentityType(type) && !IsSupportingType(type) && type != VerificationDocumentType.Other)
            throw new ApiFlowException(StatusCodes.Status400BadRequest, "DOCUMENT_SIDE_INVALID", "Loại tài liệu không được hỗ trợ trong luồng đăng ký.");

        var header = content.AsSpan(0, Math.Min(8, content.Length));
        var allowed = IsIdentityType(type) ? ImageContentTypes : DocumentContentTypes;
        var normalizedContentType = NormalizeContentType(contentType);
        var detected = DetectContentType(header);
        if (detected is null || !allowed.Contains(detected))
            throw new ApiFlowException(StatusCodes.Status415UnsupportedMediaType, "UNSUPPORTED_FILE_TYPE", "Tệp phải là JPEG, PNG hoặc PDF hợp lệ.");
        if (normalizedContentType is not null && !string.Equals(normalizedContentType, detected, StringComparison.Ordinal))
            throw new ApiFlowException(StatusCodes.Status415UnsupportedMediaType, "UNSUPPORTED_FILE_TYPE", "Định dạng tệp không khớp với nội dung.");

        var maxBytes = type switch
        {
            VerificationDocumentType.FaceVerification => upload.SelfieMaxBytes,
            VerificationDocumentType.CitizenIdFront or VerificationDocumentType.CitizenIdBack => upload.IdentityMaxBytes,
            _ => upload.SupportingDocumentMaxBytes
        };
        if (byteSize <= 0)
            throw new ApiFlowException(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Tệp rỗng.");
        if (byteSize > maxBytes)
            throw new ApiFlowException(StatusCodes.Status413PayloadTooLarge, "FILE_TOO_LARGE", $"Tệp vượt quá giới hạn {maxBytes} byte.");
    }

    private static string? NormalizeContentType(string? contentType) =>
        string.IsNullOrWhiteSpace(contentType) ? null : contentType.Split(';')[0].Trim().ToLowerInvariant();

    private static string? DetectContentType(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return "image/jpeg";
        if (header.Length >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            return "image/png";
        if (header.Length >= 4 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46)
            return "application/pdf";
        return null;
    }
}
