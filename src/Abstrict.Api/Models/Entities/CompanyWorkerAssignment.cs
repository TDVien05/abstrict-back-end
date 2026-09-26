namespace Abstrict.Api.Models.Entities;

public sealed class CompanyWorkerAssignment : Entity
{
    public Guid BookingId { get; set; }
    public Guid CompanyWorkerId { get; set; }
    public Guid AssignedByUserId { get; set; }
    public DateTimeOffset AssignedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public Booking Booking { get; set; } = null!;
    public CompanyWorker CompanyWorker { get; set; } = null!;
}
