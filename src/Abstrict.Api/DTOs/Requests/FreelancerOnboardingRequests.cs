using System.ComponentModel.DataAnnotations;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.DTOs.Requests;

public sealed class UpdateFreelancerOnboardingRequest
{
    [StringLength(160, MinimumLength = 2)]
    public string? LegalFullName { get; init; }

    public DateOnly? DateOfBirth { get; init; }
    public Gender? Gender { get; init; }

    [StringLength(400)]
    public string? PermanentAddress { get; init; }

    [StringLength(400)]
    public string? CurrentAddress { get; init; }

    [Range(0, 80)]
    public int? ExperienceYears { get; init; }

    public IReadOnlyList<Guid>? ServiceCategoryIds { get; init; }
    public IReadOnlyList<Guid>? ServiceAreaIds { get; init; }
    public OnboardingStep? CurrentStep { get; init; }

    [StringLength(40)]
    public string? BankCode { get; init; }

    [StringLength(64)]
    public string? BankAccountNumber { get; init; }

    [StringLength(160)]
    public string? BankAccountHolderName { get; init; }

    [StringLength(400)]
    public string? BankAccountHolderNameOverrideReason { get; init; }
}

public sealed class RecordKycConsentRequest
{
    [Required]
    public KycConsentType? ConsentType { get; init; }

    [Required, StringLength(60, MinimumLength = 1)]
    public string ContentVersion { get; init; } = string.Empty;

    public bool Accepted { get; init; } = true;
}

public sealed class UploadOnboardingDocumentRequest
{
    [Required]
    public VerificationDocumentType? Type { get; init; }

    [StringLength(200)]
    public string? Issuer { get; init; }

    public DateOnly? IssuedOn { get; init; }
    public DateOnly? ExpiresOn { get; init; }
}

public sealed class StartIdentityOcrRequest
{
    [Required]
    public Guid FrontDocumentId { get; init; }

    [Required]
    public Guid BackDocumentId { get; init; }
}

public sealed class ConfirmIdentityRequest
{
    [Required]
    public Guid AttemptId { get; init; }

    public IReadOnlyDictionary<string, string?>? Corrections { get; init; }

    [StringLength(500)]
    public string? CorrectionReason { get; init; }
}

public sealed class StartFaceMatchRequest
{
    [Required]
    public Guid SelfieDocumentId { get; init; }

    [Required]
    public Guid AttemptId { get; init; }
}

public sealed class SubmitApplicationRequest
{
    [Required, StringLength(60, MinimumLength = 1)]
    public string FinalConsentVersion { get; init; } = string.Empty;
}

public sealed class ReopenApplicationRequest
{
    [StringLength(500)]
    public string? Reason { get; init; }
}
