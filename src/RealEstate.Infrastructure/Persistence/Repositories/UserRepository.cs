using Microsoft.EntityFrameworkCore;
using Npgsql;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;

namespace RealEstate.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private const string NormalizedEmailUniqueIndexName =
        "IX_Users_NormalizedEmail";

    private readonly RealEstateDbContext _dbContext;

    public UserRepository(RealEstateDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Users.SingleOrDefaultAsync(
            user => user.NormalizedEmail == normalizedEmail,
            cancellationToken);
    }

    public async Task<User?> GetByNormalizedEmailReadOnlyAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.NormalizedEmail == normalizedEmail,
                cancellationToken);
    }

    public async Task<bool> ExistsByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Users.AnyAsync(
            user => user.NormalizedEmail == normalizedEmail,
            cancellationToken);
    }

    public async Task<User?> GetByIdReadOnlyAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public async Task<User?> GetByIdForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public async Task AddAsync(
        User user,
        CancellationToken cancellationToken)
    {
        await _dbContext.Users.AddAsync(user, cancellationToken);
    }

    public async Task<UserRegistrationPersistenceResult>
        PersistRegistrationAsync(
            User user,
            CancellationToken cancellationToken)
    {
        await _dbContext.Users.AddAsync(
            user,
            cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(
                cancellationToken);

            return UserRegistrationPersistenceResult.Succeeded;
        }
        catch (DbUpdateException exception)
            when (IsNormalizedEmailUniqueViolation(exception))
        {
            _dbContext.ChangeTracker.Clear();

            return UserRegistrationPersistenceResult
                .NormalizedEmailAlreadyExists;
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsNormalizedEmailUniqueViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgresException &&
            postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
            string.Equals(
                postgresException.ConstraintName,
                NormalizedEmailUniqueIndexName,
                StringComparison.Ordinal);
    }
}
