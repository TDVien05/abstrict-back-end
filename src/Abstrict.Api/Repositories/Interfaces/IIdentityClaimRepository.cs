using Abstrict.Api.Models.Entities;

namespace Abstrict.Api.Repositories.Interfaces;

public interface IIdentityClaimRepository
{
    Task<IdentityClaim?> GetByFingerprintAsync(string fingerprint, bool track, CancellationToken cancellationToken);
    Task AddAsync(IdentityClaim claim, CancellationToken cancellationToken);
}
