using Abstrict.Api.Models.Entities;

namespace Abstrict.Api.Repositories.Interfaces;

public interface IApplicationSubmissionRepository
{
    Task<ApplicationSubmission?> GetLatestAsync(Guid applicationId, CancellationToken cancellationToken);
    Task<ApplicationSubmission?> GetByVersionAsync(Guid applicationId, int version, bool track, CancellationToken cancellationToken);
    Task AddAsync(ApplicationSubmission submission, CancellationToken cancellationToken);
}
