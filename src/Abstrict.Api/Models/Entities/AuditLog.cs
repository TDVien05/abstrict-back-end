namespace Abstrict.Api.Models.Entities;

public sealed class AuditLog : Entity
{
    public Guid? ActorUserId { get; set; }
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? CorrelationId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
