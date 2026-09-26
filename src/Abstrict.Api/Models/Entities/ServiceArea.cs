namespace Abstrict.Api.Models.Entities;

public sealed class ServiceArea : Entity
{
    public required string City { get; set; }
    public required string District { get; set; }
    public string? WardOrComplex { get; set; }
    public bool IsActive { get; set; } = true;
}
