using Abstrict.Api.Models.Entities;

namespace Abstrict.Api.Repositories.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByPhoneNumberAsync(string phoneNumber, bool track, CancellationToken cancellationToken);
    Task<User?> GetByIdAsync(Guid id, bool track, CancellationToken cancellationToken);
    Task AddAsync(User user, CancellationToken cancellationToken);
}
