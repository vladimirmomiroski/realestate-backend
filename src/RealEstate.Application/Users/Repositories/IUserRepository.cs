using RealEstate.Domain.Entities;

namespace RealEstate.Application.Users.Repositories;

public interface IUserRepository
{
    Task<bool> ExistsByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task<User?> GetByNormalizedEmailAsync(
    string normalizedEmail,
    CancellationToken cancellationToken);

    Task<User?> GetByNormalizedEmailReadOnlyAsync(
    string normalizedEmail,
    CancellationToken cancellationToken);

    Task<User?> GetByIdReadOnlyAsync(
    Guid id,
    CancellationToken cancellationToken);

    Task<User?> GetByIdForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task AddAsync(
        User user,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
