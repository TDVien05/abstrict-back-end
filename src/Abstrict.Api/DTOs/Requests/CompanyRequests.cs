using System.ComponentModel.DataAnnotations;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.DTOs.Requests;

public sealed class SaveCompanyProfileRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string LegalName { get; init; } = string.Empty;

    /// <summary>Tên hiển thị với khách hàng.</summary>
    [Required, StringLength(120, MinimumLength = 2)]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Mã số thuế: 10 chữ số, hoặc 10 chữ số + "-" + 3 chữ số.</summary>
    [Required, StringLength(20, MinimumLength = 10)]
    public string TaxCode { get; init; } = string.Empty;

    [Required, StringLength(24, MinimumLength = 8)]
    public string Hotline { get; init; } = string.Empty;

    [Required, StringLength(300, MinimumLength = 5)]
    public string HeadquartersAddress { get; init; } = string.Empty;

    public CompanyWorkforceSize WorkforceSize { get; init; }

    /// <summary>Mức bảo hiểm trách nhiệm (VND). Lớn hơn 0 thì bắt buộc nộp giấy tờ <c>LiabilityInsurance</c>.</summary>
    [Range(0, long.MaxValue)]
    public long? LiabilityCoverageVnd { get; init; }
}

public sealed class SaveCompanyRepresentativeRequest
{
    [Required, StringLength(160, MinimumLength = 2)]
    public string FullName { get; init; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 2)]
    public string JobTitle { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string WorkEmail { get; init; } = string.Empty;

    [Required, StringLength(24, MinimumLength = 9)]
    public string PhoneNumber { get; init; } = string.Empty;

    [Required, RegularExpression(@"^\d{12}$", ErrorMessage = "Số CCCD gồm đúng 12 chữ số.")]
    public string IdentityNumber { get; init; } = string.Empty;
}

public sealed class SaveCompanyServiceAreasRequest
{
    [Required, MinLength(1), MaxLength(20)]
    public IReadOnlyCollection<Guid> ServiceAreaIds { get; init; } = [];
}
