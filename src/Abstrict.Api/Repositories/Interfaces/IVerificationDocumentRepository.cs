using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Repositories.Interfaces;

public interface IVerificationDocumentRepository
{
    Task<VerificationDocument?> GetAsync(Guid id, bool track, CancellationToken cancellationToken);
    Task<VerificationDocument?> GetByObjectKeyAsync(string objectKey, bool track, CancellationToken cancellationToken);
    Task<VerificationDocument?> GetCurrentAsync(Guid applicationId, VerificationDocumentType type, CancellationToken cancellationToken);
    Task<IReadOnlyList<VerificationDocument>> ListByApplicationAsync(Guid applicationId, bool track, CancellationToken cancellationToken);
    Task<IReadOnlyList<VerificationDocument>> ListCurrentByApplicationAsync(Guid applicationId, CancellationToken cancellationToken);
    Task AddAsync(VerificationDocument document, CancellationToken cancellationToken);
    Task AddRangeAsync(IEnumerable<VerificationDocument> documents, CancellationToken cancellationToken);
}
