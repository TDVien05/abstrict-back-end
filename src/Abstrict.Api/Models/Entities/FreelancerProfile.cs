using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class FreelancerProfile : Entity
{
    public Guid ProviderId { get; set; }
    public Guid UserId { get; set; }
    public required string LegalFullName { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public required string PermanentAddress { get; set; }
    public required string CurrentAddress { get; set; }
    public int ExperienceYears { get; set; }
    public bool AutoAcceptBookings { get; set; }
    public decimal? FaceMatchScore { get; set; }
    public Provider Provider { get; set; } = null!;
    public User User { get; set; } = null!;
}
