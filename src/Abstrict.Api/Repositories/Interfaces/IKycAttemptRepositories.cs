using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Repositories.Interfaces;

public interface IIdentityAttemptRepository
{
    Task<IdentityVerificationAttempt?> GetAsync(Guid id, bool track, CancellationToken cancellationToken);
    Task<IdentityVerificationAttempt?> GetCurrentAsync(Guid applicationId, bool track, CancellationToken cancellationToken);
    Task AddAsync(IdentityVerificationAttempt attempt, CancellationToken cancellationToken);
}

public interface IKycConsentRepository
{
    Task<KycConsent?> GetActiveAsync(Guid userId, KycConsentType type, CancellationToken cancellationToken);
    Task AddAsync(KycConsent consent, CancellationToken cancellationToken);
}
