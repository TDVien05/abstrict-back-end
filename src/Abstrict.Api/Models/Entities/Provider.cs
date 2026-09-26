using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class Provider : Entity
{
    public ProviderType Type { get; set; }
    public required string DisplayName { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;
    public bool IsAcceptingBookings { get; set; }
    public string? AvatarObjectKey { get; set; }
    public string? Bio { get; set; }
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }
    public int CompletedBookingCount { get; set; }
    public FreelancerProfile? FreelancerProfile { get; set; }
    public CompanyProfile? CompanyProfile { get; set; }
    public ICollection<ProviderService> Services { get; set; } = new List<ProviderService>();
    public ICollection<ProviderServiceArea> ServiceAreas { get; set; } = new List<ProviderServiceArea>();
    public ICollection<AvailabilityWindow> AvailabilityWindows { get; set; } = new List<AvailabilityWindow>();
}
