using System.ComponentModel.DataAnnotations;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.DTOs.Requests;

public sealed class SaveFreelancerKycProfileRequest
{
    [Required, StringLength(160, MinimumLength = 2)]
    public string LegalFullName { get; init; } = string.Empty;

    public DateOnly DateOfBirth { get; init; }

    public Gender Gender { get; init; }

    [Required, RegularExpression(@"^\d{12}$", ErrorMessage = "Số CCCD gồm đúng 12 chữ số.")]
    public string CitizenIdNumber { get; init; } = string.Empty;

    [Required, StringLength(300, MinimumLength = 5)]
    public string PermanentAddress { get; init; } = string.Empty;

    [Required, StringLength(300, MinimumLength = 5)]
    public string CurrentAddress { get; init; } = string.Empty;

    [Range(0, 60)]
    public int ExperienceYears { get; init; }
}

public sealed class KycDecisionRequest
{
    /// <summary>Approved, Rejected hoặc ChangesRequested.</summary>
    public ProviderApprovalDecision Decision { get; init; }

    [StringLength(1000)]
    public string? Reason { get; init; }

    /// <summary>Các loại giấy tờ cần nộp lại; chỉ áp dụng khi từ chối hoặc yêu cầu chỉnh sửa.</summary>
    public IReadOnlyCollection<VerificationDocumentType>? RejectedDocumentTypes { get; init; }
}
