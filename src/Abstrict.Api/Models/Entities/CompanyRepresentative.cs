namespace Abstrict.Api.Models.Entities;

public sealed class CompanyRepresentative : Entity
{
    public Guid CompanyProfileId { get; set; }
    public required string FullName { get; set; }
    public required string JobTitle { get; set; }
    public required string WorkEmail { get; set; }
    public required string PhoneNumber { get; set; }
    public required string IdentityNumberEncrypted { get; set; }
    public CompanyProfile CompanyProfile { get; set; } = null!;
}
