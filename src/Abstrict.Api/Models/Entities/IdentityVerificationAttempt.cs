using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class IdentityVerificationAttempt : Entity
{
    public Guid ApplicationId { get; set; }
    public int ApplicationVersion { get; set; }
    public Guid? FrontDocumentId { get; set; }
    public Guid? BackDocumentId { get; set; }
    public Guid? SelfieDocumentId { get; set; }

    public OcrState OcrState { get; set; } = OcrState.NotStarted;
    public FaceState FaceState { get; set; } = FaceState.NotStarted;
    public LivenessState LivenessState { get; set; } = LivenessState.NotPerformed;

    public string? DocumentNumberEncrypted { get; set; }
    public string? DocumentNumberLast4 { get; set; }
    public string? DocumentNumberFingerprint { get; set; }
    public string? FullName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public string? PermanentAddress { get; set; }
    public DateOnly? IssuedOn { get; set; }
    public string? IssuedPlace { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public string? DocumentType { get; set; }

    public string? FieldConfidencesJson { get; set; }
    public string? CorrectionsJson { get; set; }

    public decimal? FaceScore { get; set; }
    public decimal? FaceThreshold { get; set; }
    public string? ResultCode { get; set; }

    public string? OcrProviderRequestId { get; set; }
    public string? FaceProviderRequestId { get; set; }
    public string? PolicyVersion { get; set; }

    public DateTimeOffset? OcrCompletedAtUtc { get; set; }
    public DateTimeOffset? FaceCompletedAtUtc { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
    public DateTimeOffset? SupersededAtUtc { get; set; }

    public FreelancerApplication Application { get; set; } = null!;

    public bool IsCurrent => SupersededAtUtc is null;
}
