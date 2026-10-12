using System.Text.RegularExpressions;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Services.Implementations;

public static partial class CompanyTaxCode
{
    /// <summary>Chuẩn hóa mã số thuế: 10 chữ số, hoặc 10 chữ số + "-" + 3 chữ số (chi nhánh). Trả <c>null</c> nếu sai.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;
        var compact = Whitespace().Replace(input, string.Empty);
        return Pattern().IsMatch(compact) ? compact : null;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^\d{10}(-\d{3})?$")]
    private static partial Regex Pattern();
}

public static partial class CompanyHotline
{
    /// <summary>Số di động/cố định Việt Nam, hoặc tổng đài 1800/1900 (8–10 chữ số). Trả <c>null</c> nếu sai.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;
        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (AllowedCharacters().IsMatch(input) && TollFree().IsMatch(digits))
            return digits;
        return VietnamesePhoneNumber.Normalize(input);
    }

    [GeneratedRegex(@"^[0-9\s.\-]+$")]
    private static partial Regex AllowedCharacters();

    [GeneratedRegex(@"^1[89]00\d{4,6}$")]
    private static partial Regex TollFree();
}

/// <summary>Định dạng giấy tờ của công ty, xác định từ nội dung thực (magic bytes).</summary>
public static class CompanyDocument
{
    public const long MaximumBytes = 10 * 1024 * 1024;

    public static readonly VerificationDocumentType[] AllTypes =
    [
        VerificationDocumentType.BusinessRegistration,
        VerificationDocumentType.CitizenIdFront,
        VerificationDocumentType.CitizenIdBack,
        VerificationDocumentType.LiabilityInsurance
    ];

    public static bool AllowsPdf(VerificationDocumentType type) =>
        type is VerificationDocumentType.BusinessRegistration or VerificationDocumentType.LiabilityInsurance;

    public static (string ContentType, string Extension)? Detect(VerificationDocumentType type, ReadOnlySpan<byte> header)
    {
        if (KycImage.Detect(header) is { } image)
            return image;
        if (AllowsPdf(type) && header.Length >= 5 && header[..5].SequenceEqual("%PDF-"u8))
            return ("application/pdf", ".pdf");
        return null;
    }

    public static string ContentTypeFromKey(string objectKey) =>
        objectKey.EndsWith(".pdf", StringComparison.Ordinal) ? "application/pdf" : KycImage.ContentTypeFromKey(objectKey);
}

public static class CompanyOnboardingPolicy
{
    public static bool IsEditable(ApprovalStatus status) => status is ApprovalStatus.Draft or ApprovalStatus.Rejected;

    /// <summary>Bảo hiểm trách nhiệm chỉ bắt buộc khi công ty khai mức bảo hiểm lớn hơn 0.</summary>
    public static IReadOnlyList<VerificationDocumentType> RequiredDocumentTypes(long? liabilityCoverageVnd)
    {
        var types = new List<VerificationDocumentType>
        {
            VerificationDocumentType.BusinessRegistration,
            VerificationDocumentType.CitizenIdFront,
            VerificationDocumentType.CitizenIdBack
        };
        if (liabilityCoverageVnd > 0)
            types.Add(VerificationDocumentType.LiabilityInsurance);
        return types;
    }

    public static bool HasAllRequiredDocuments(long? liabilityCoverageVnd, IEnumerable<VerificationDocumentType> uploaded)
    {
        var present = uploaded.ToHashSet();
        return RequiredDocumentTypes(liabilityCoverageVnd).All(present.Contains);
    }

    /// <summary>Bước tiếp theo công ty cần làm; <c>status</c> là <c>null</c> khi chưa lưu thông tin pháp nhân.</summary>
    public static CompanyOnboardingStep ResolveStep(
        ApprovalStatus? status, bool hasRepresentative, bool hasServiceAreas, long? liabilityCoverageVnd,
        IReadOnlyCollection<(VerificationDocumentType Type, VerificationStatus Status)> documents) => status switch
        {
            ApprovalStatus.Approved => CompanyOnboardingStep.Completed,
            ApprovalStatus.Suspended => CompanyOnboardingStep.Suspended,
            ApprovalStatus.Submitted or ApprovalStatus.UnderReview => CompanyOnboardingStep.AwaitingReview,
            null => CompanyOnboardingStep.Company,
            _ when !hasRepresentative => CompanyOnboardingStep.Representative,
            _ when !hasServiceAreas => CompanyOnboardingStep.ServiceAreas,
            _ when !HasAllRequiredDocuments(liabilityCoverageVnd, documents.Select(x => x.Type))
                || documents.Any(x => x.Status == VerificationStatus.Rejected) => CompanyOnboardingStep.Documents,
            _ => CompanyOnboardingStep.Submit
        };
}
