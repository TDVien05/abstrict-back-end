using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class VerificationDocument : Entity
{
    public Guid ProviderId { get; set; }
    public VerificationDocumentType Type { get; set; }
    public VerificationStatus Status { get; set; } = VerificationStatus.Pending;
    public required string ObjectKey { get; set; }
    public required string OriginalFileName { get; set; }
    public string? Issuer { get; set; }
    public DateOnly? IssuedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public string? ExtractedDataJson { get; set; }
    public DateTimeOffset? VerifiedAtUtc { get; set; }
    public Guid? VerifiedByAdminUserId { get; set; }
    public string? RejectionReason { get; set; }
    public Provider Provider { get; set; } = null!;
}
