using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class CompanyProfile : Entity
{
    public Guid ProviderId { get; set; }
    public Guid OwnerUserId { get; set; }
    public required string LegalName { get; set; }
    public required string TaxCode { get; set; }
    public required string Hotline { get; set; }
    public required string HeadquartersAddress { get; set; }
    public CompanyWorkforceSize WorkforceSize { get; set; }
    public long? LiabilityCoverageVnd { get; set; }
    public Provider Provider { get; set; } = null!;
    public User OwnerUser { get; set; } = null!;
    public CompanyRepresentative? Representative { get; set; }
    public ICollection<CompanyWorker> Workers { get; set; } = new List<CompanyWorker>();
}
