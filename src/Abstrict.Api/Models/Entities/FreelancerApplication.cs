using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class FreelancerApplication : Entity
{
    public Guid UserId { get; set; }
    public Guid ProviderId { get; set; }
    public int Version { get; set; } = 1;
    public FreelancerApplicationStatus Status { get; set; } = FreelancerApplicationStatus.Draft;
    public OnboardingStep CurrentStep { get; set; } = OnboardingStep.Personal;

    public string? LegalFullName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public string? PermanentAddress { get; set; }
    public string? CurrentAddress { get; set; }
    public int? ExperienceYears { get; set; }

    public string? ServiceCategoryIdsJson { get; set; }
    public string? ServiceAreaIdsJson { get; set; }

    public string? BankCode { get; set; }
    public string? BankAccountNumberEncrypted { get; set; }
    public string? BankAccountNumberLast4 { get; set; }
    public string? BankAccountHolderName { get; set; }
    public bool BankAccountHolderNameOverridden { get; set; }

    public string? ConfirmedIdentityFullName { get; set; }
    public string? ConfirmedIdentityDocumentNumberLast4 { get; set; }

    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public int? SubmittedVersion { get; set; }
    public string? LastDecisionReason { get; set; }

    public User User { get; set; } = null!;
    public Provider Provider { get; set; } = null!;
}
