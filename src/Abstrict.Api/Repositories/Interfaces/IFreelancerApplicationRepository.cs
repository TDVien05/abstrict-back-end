using Abstrict.Api.Models.Entities;

namespace Abstrict.Api.Repositories.Interfaces;

public interface IFreelancerApplicationRepository
{
    Task<FreelancerApplication?> GetByUserIdAsync(Guid userId, bool track, CancellationToken cancellationToken);
    Task<FreelancerApplication?> GetByIdAsync(Guid id, bool track, CancellationToken cancellationToken);
    Task<FreelancerApplication?> GetByProviderIdAsync(Guid providerId, bool track, CancellationToken cancellationToken);
    Task AddAsync(FreelancerApplication application, CancellationToken cancellationToken);
    Task<IReadOnlyList<FreelancerApplication>> ListByStatusAsync(string? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<long> CountByStatusAsync(string? status, CancellationToken cancellationToken);
}
