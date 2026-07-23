using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Infrastructure.Persistence.Repositories;

public sealed class AgencyInvitationRepository
    : IAgencyInvitationRepository
{
    private const string AgencyMemberUniqueIndexName =
        "IX_AgencyMembers_AgencyId_UserId";

    private readonly RealEstateDbContext _dbContext;

    public AgencyInvitationRepository(
        RealEstateDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CreateAsync(
        AgencyInvitation invitation,
        CancellationToken cancellationToken)
    {
        _dbContext.AgencyInvitations.Add(invitation);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public Task<IAgencyInvitationTerminalMutationScope?>
        BeginTerminalMutationByTokenAsync(
            string token,
            CancellationToken cancellationToken)
    {
        return BeginTerminalMutationAsync(
            commandText:
                """
                SELECT "Id"
                FROM "AgencyInvitations"
                WHERE "Token" = @token
                FOR UPDATE;
                """,
            parameterName: "token",
            parameterType: DbType.String,
            parameterValue: token,
            cancellationToken);
    }

    public Task<IAgencyInvitationTerminalMutationScope?>
        BeginTerminalMutationByIdAsync(
            Guid invitationId,
            CancellationToken cancellationToken)
    {
        return BeginTerminalMutationAsync(
            commandText:
                """
                SELECT "Id"
                FROM "AgencyInvitations"
                WHERE "Id" = @invitationId
                FOR UPDATE;
                """,
            parameterName: "invitationId",
            parameterType: DbType.Guid,
            parameterValue: invitationId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<AgencyInvitation>>
        GetByAgencyIdReadOnlyAsync(
            Guid agencyId,
            AgencyInvitationStatus? status,
            CancellationToken cancellationToken)
    {
        IQueryable<AgencyInvitation> query =
            _dbContext.AgencyInvitations
                .AsNoTracking()
                .Where(invitation =>
                    invitation.AgencyId == agencyId);

        if (status.HasValue)
        {
            query = query.Where(invitation =>
                invitation.Status == status.Value);
        }

        return await query
            .OrderByDescending(invitation =>
                invitation.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool>
        ExistsPendingForAgencyEmailAsync(
            Guid agencyId,
            string normalizedEmail,
            CancellationToken cancellationToken)
    {
        return await _dbContext.AgencyInvitations
            .AnyAsync(
                invitation =>
                    invitation.AgencyId == agencyId &&
                    invitation.NormalizedEmail ==
                        normalizedEmail &&
                    invitation.Status ==
                        AgencyInvitationStatus.Pending,
                cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    private async Task<
        IAgencyInvitationTerminalMutationScope?>
        BeginTerminalMutationAsync(
            string commandText,
            string parameterName,
            DbType parameterType,
            object parameterValue,
            CancellationToken cancellationToken)
    {
        IDbContextTransaction transaction =
            await _dbContext.Database
                .BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

        try
        {
            DbConnection connection =
                _dbContext.Database.GetDbConnection();

            await using DbCommand command =
                connection.CreateCommand();

            command.Transaction =
                transaction.GetDbTransaction();

            command.CommandText = commandText;

            DbParameter parameter =
                command.CreateParameter();

            parameter.ParameterName =
                parameterName;

            parameter.DbType =
                parameterType;

            parameter.Value =
                parameterValue;

            command.Parameters.Add(parameter);

            object? lockedInvitationId =
                await command.ExecuteScalarAsync(
                    cancellationToken);

            if (lockedInvitationId is null ||
                lockedInvitationId is DBNull)
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);

                await transaction.DisposeAsync();

                return null;
            }

            Guid invitationId =
                (Guid)lockedInvitationId;

            AgencyInvitation invitation =
                await _dbContext.AgencyInvitations
                    .SingleAsync(
                        invitation =>
                            invitation.Id ==
                            invitationId,
                        cancellationToken);

            return new AgencyInvitationTerminalMutationScope(
                _dbContext,
                transaction,
                invitation);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);
            }
            finally
            {
                await transaction.DisposeAsync();
            }

            throw;
        }
    }

    private sealed class
        AgencyInvitationTerminalMutationScope
        : IAgencyInvitationTerminalMutationScope
    {
        private readonly RealEstateDbContext _dbContext;
        private readonly IDbContextTransaction _transaction;

        private bool _committed;
        private bool _disposed;

        public AgencyInvitationTerminalMutationScope(
            RealEstateDbContext dbContext,
            IDbContextTransaction transaction,
            AgencyInvitation invitation)
        {
            _dbContext = dbContext;
            _transaction = transaction;
            Invitation = invitation;
        }

        public AgencyInvitation Invitation { get; }

        public async Task
            PersistTerminalTransitionAsync(
                CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }

        public async Task<
            AgencyInvitationAcceptancePersistenceResult>
            PersistAcceptanceAsync(
                CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            try
            {
                await _dbContext.SaveChangesAsync(
                    cancellationToken);

                return
                    AgencyInvitationAcceptancePersistenceResult
                        .Succeeded;
            }
            catch (DbUpdateException exception)
                when (IsAgencyMembershipUniqueViolation(
                    exception))
            {
                await CompleteFailedAcceptanceAsync();

                return
                    AgencyInvitationAcceptancePersistenceResult
                        .MembershipAlreadyExists;
            }
        }

        public async Task CommitAsync(
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_committed)
            {
                return;
            }

            await _transaction.CommitAsync(
                cancellationToken);

            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (!_committed)
                {
                    await _transaction.RollbackAsync(
                        CancellationToken.None);
                }
            }
            finally
            {
                try
                {
                    await _transaction.DisposeAsync();
                }
                finally
                {
                    _disposed = true;
                }
            }
        }

        private async Task
            CompleteFailedAcceptanceAsync()
        {
            try
            {
                await _transaction.RollbackAsync(
                    CancellationToken.None);
            }
            finally
            {
                try
                {
                    await _transaction.DisposeAsync();
                }
                finally
                {
                    _dbContext.ChangeTracker.Clear();
                    _disposed = true;
                }
            }
        }

        private static bool
            IsAgencyMembershipUniqueViolation(
                DbUpdateException exception)
        {
            return exception.InnerException
                    is PostgresException postgresException &&
                postgresException.SqlState ==
                    PostgresErrorCodes.UniqueViolation &&
                string.Equals(
                    postgresException.ConstraintName,
                    AgencyMemberUniqueIndexName,
                    StringComparison.Ordinal);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(
                        AgencyInvitationTerminalMutationScope));
            }
        }
    }
}
