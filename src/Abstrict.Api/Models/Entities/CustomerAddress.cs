namespace Abstrict.Api.Models.Entities;

public sealed class CustomerAddress : Entity
{
    public Guid CustomerUserId { get; set; }
    public required string Label { get; set; }
    public required string ApartmentNumber { get; set; }
    public string? Floor { get; set; }
    public required string BuildingName { get; set; }
    public required string StreetAddress { get; set; }
    public required string Ward { get; set; }
    public required string District { get; set; }
    public required string City { get; set; }
    public string? AccessInstructions { get; set; }
    public bool IsDefault { get; set; }
    public User CustomerUser { get; set; } = null!;
}
