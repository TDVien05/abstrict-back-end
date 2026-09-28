using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Repositories.Interfaces;

public interface IKycOperationRepository
{
    Task<KycOperation?> GetAsync(Guid id, bool track, CancellationToken cancellationToken);
    Task<KycOperation?> FindByIdempotencyKeyAsync(Guid userId, KycOperationType type, string idempotencyKey, CancellationToken cancellationToken);
    Task AddAsync(KycOperation operation, CancellationToken cancellationToken);
    Task<KycOperation?> ClaimNextAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
