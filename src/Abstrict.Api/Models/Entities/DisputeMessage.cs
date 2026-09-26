namespace Abstrict.Api.Models.Entities;

public sealed class DisputeMessage : Entity
{
    public Guid DisputeId { get; set; }
    public Guid AuthorUserId { get; set; }
    public required string Body { get; set; }
    public bool IsInternalAdminNote { get; set; }
    public Dispute Dispute { get; set; } = null!;
}
