using System.Text.Json;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Options;

namespace Abstrict.Api.Services.Implementations;

public static class FreelancerOnboardingMapper
{
    public static IReadOnlyList<Guid> ReadIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static FreelancerOnboardingResponse ToResponse(
        FreelancerApplication application,
        IdentityVerificationAttempt? attempt,
        IReadOnlyList<VerificationDocument> documents,
        ApplicationSubmission? latestSubmission,
        KycPolicyOptions policy)
    {
        var personal = new FreelancerPersonalInfoResponse(
            application.LegalFullName,
            application.DateOfBirth,
            application.Gender?.ToString(),
            application.PermanentAddress,
            application.CurrentAddress,
            application.ExperienceYears);

        var identity = new FreelancerIdentityResponse(
            attempt?.Id,
            attempt?.FrontDocumentId,
            attempt?.BackDocumentId,
            attempt?.SelfieDocumentId,
            (attempt?.OcrState ?? OcrState.NotStarted).ToString(),
            (attempt?.FaceState ?? FaceState.NotStarted).ToString(),
            (attempt?.LivenessState ?? LivenessState.NotPerformed).ToString(),
            attempt?.DocumentNumberLast4 is { Length: > 0 } last4 ? $"********{last4}" : null,
            attempt?.FullName,
            attempt?.DateOfBirth,
            attempt?.Gender?.ToString(),
            attempt?.PermanentAddress,
            attempt?.IssuedOn,
            attempt?.IssuedPlace,
            attempt?.ExpiresOn,
            attempt?.FaceScore,
            attempt?.FaceThreshold,
            attempt?.ResultCode,
            attempt?.ConfirmedAtUtc,
            attempt is not null && attempt.ConfirmedAtUtc is null && attempt.OcrState is OcrState.Extracted or OcrState.NeedsReview);

        var documentResponses = documents
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new FreelancerDocumentResponse(
                x.Id,
                x.Type.ToString(),
                x.Revision,
                x.Status.ToString(),
                x.ScanStatus.ToString(),
                x.OriginalFileName,
                x.ContentType,
                x.ByteSize,
                x.CreatedAtUtc,
                x.SupersededAtUtc is null))
            .ToList();

        var skillsAndBank = new FreelancerSkillsAndBankResponse(
            ReadIds(application.ServiceCategoryIdsJson),
            ReadIds(application.ServiceAreaIdsJson),
            new FreelancerBankInfoResponse(
                application.BankCode,
                application.BankAccountNumberLast4 is { Length: > 0 } bankLast4 ? $"****{bankLast4}" : null,
                application.BankAccountHolderName,
                application.BankAccountHolderNameOverridden,
                false));

        var requirements = new FreelancerOnboardingRequirementsResponse(
            policy.Version,
            policy.RequireHealthCertificate,
            policy.RequireCriminalRecord,
            policy.AllowManualReview,
            policy.MaxFaceAttemptsPerDay,
            policy.LivenessMode);

        FreelancerSubmissionSummaryResponse? submission = latestSubmission is null
            ? null
            : new FreelancerSubmissionSummaryResponse(
                latestSubmission.Id,
                latestSubmission.Version,
                latestSubmission.Status.ToString(),
                latestSubmission.Decision.ToString(),
                latestSubmission.SubmittedAtUtc,
                latestSubmission.DecidedAtUtc,
                latestSubmission.DecisionReason,
                ApplicationCode(latestSubmission));

        return new FreelancerOnboardingResponse(
            application.Id,
            application.Version,
            application.Status.ToString(),
            application.CurrentStep.ToString(),
            personal,
            identity,
            documentResponses,
            skillsAndBank,
            ComputeMissingFields(application, attempt, documents, policy),
            requirements,
            submission,
            application.LastDecisionReason);
    }

    public static string ApplicationCode(ApplicationSubmission submission) =>
        $"FR-{submission.SubmittedAtUtc:yyyyMMdd}-{submission.Id.ToString("N")[..8].ToUpperInvariant()}";

    public static IReadOnlyList<string> ComputeMissingFields(
        FreelancerApplication application,
        IdentityVerificationAttempt? attempt,
        IReadOnlyList<VerificationDocument> documents,
        KycPolicyOptions policy)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(application.LegalFullName)) missing.Add("legalFullName");
        if (application.DateOfBirth is null) missing.Add("dateOfBirth");
        if (application.Gender is null) missing.Add("gender");
        if (string.IsNullOrWhiteSpace(application.PermanentAddress)) missing.Add("permanentAddress");
        if (string.IsNullOrWhiteSpace(application.CurrentAddress)) missing.Add("currentAddress");
        if (ReadIds(application.ServiceCategoryIdsJson).Count == 0) missing.Add("serviceCategoryIds");
        if (ReadIds(application.ServiceAreaIdsJson).Count == 0) missing.Add("serviceAreaIds");
        if (string.IsNullOrWhiteSpace(application.BankCode)) missing.Add("bankCode");
        if (string.IsNullOrWhiteSpace(application.BankAccountNumberEncrypted)) missing.Add("bankAccountNumber");

        var current = CurrentDocuments(documents);
        if (attempt?.FrontDocumentId is null && !current.Any(x => x.Type == VerificationDocumentType.CitizenIdFront))
            missing.Add("citizenIdFront");
        if (attempt?.BackDocumentId is null && !current.Any(x => x.Type == VerificationDocumentType.CitizenIdBack))
            missing.Add("citizenIdBack");
        if (attempt?.OcrState is not OcrState.Extracted and not OcrState.NeedsReview)
            missing.Add("identityOcr");
        if (attempt?.ConfirmedAtUtc is null) missing.Add("identityConfirmation");
        if (attempt?.FaceState != FaceState.Matched) missing.Add("faceMatch");
        if (policy.RequireHealthCertificate && !current.Any(x => x.Type == VerificationDocumentType.HealthCertificate))
            missing.Add("healthCertificate");
        if (policy.RequireCriminalRecord && !current.Any(x => x.Type == VerificationDocumentType.CriminalRecord))
            missing.Add("criminalRecord");

        return missing;
    }

    public static IReadOnlyList<VerificationDocument> CurrentDocuments(IEnumerable<VerificationDocument> documents) =>
        documents.Where(x => x.SupersededAtUtc is null).ToList();
}
