namespace Abstrict.Api.Models.Entities;

public sealed class DisputeEvidence : Entity
{
    public Guid DisputeId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public required string ObjectKey { get; set; }
    public string? Description { get; set; }
    public bool IsNewAppealEvidence { get; set; }
    public Dispute Dispute { get; set; } = null!;
}
