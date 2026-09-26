namespace Abstrict.Api.Models.Entities;

public sealed class CompanyWorker : Entity
{
    public Guid CompanyProfileId { get; set; }
    public required string FullName { get; set; }
    public required string PhoneNumber { get; set; }
    public string? EmployeeCode { get; set; }
    public bool IsActive { get; set; } = true;
    public CompanyProfile CompanyProfile { get; set; } = null!;
}
