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

    private const string PendingInvitationUniqueIndexName =
        "IX_AgencyInvitations_AgencyId_NormalizedEmail";

    private readonly RealEstateDbContext _dbContext;

    public AgencyInvitationRepository(
        RealEstateDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IAgencyInvitationCreationScope>
        BeginCreateOrReplaceAsync(
            Guid agencyId,
            string normalizedEmail,
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

            command.CommandText =
                """
                SELECT "Id"
                FROM "AgencyInvitations"
                WHERE "AgencyId" = @agencyId
                  AND "NormalizedEmail" = @normalizedEmail
                  AND "Status" = @pendingStatus
                FOR UPDATE;
                """;

            AddParameter(
                command,
                "agencyId",
                DbType.Guid,
                agencyId);

            AddParameter(
                command,
                "normalizedEmail",
                DbType.String,
                normalizedEmail);

            AddParameter(
                command,
                "pendingStatus",
                DbType.String,
                AgencyInvitationStatus.Pending.ToString());

            object? lockedInvitationId =
                await command.ExecuteScalarAsync(
                    cancellationToken);

            AgencyInvitation? pendingInvitation = null;

            if (lockedInvitationId is not null &&
                lockedInvitationId is not DBNull)
            {
                Guid invitationId =
                    (Guid)lockedInvitationId;

                pendingInvitation =
                    await _dbContext.AgencyInvitations
                        .SingleAsync(
                            invitation =>
                                invitation.Id ==
                                invitationId,
                            cancellationToken);
            }

            return new AgencyInvitationCreationScope(
                _dbContext,
                transaction,
                pendingInvitation);
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
            DateTime utcNow,
            CancellationToken cancellationToken)
    {
        IQueryable<AgencyInvitation> query =
            _dbContext.AgencyInvitations
                .AsNoTracking()
                .Where(invitation =>
                    invitation.AgencyId == agencyId);

        if (status == AgencyInvitationStatus.Pending)
        {
            query = query.Where(invitation =>
                invitation.Status ==
                    AgencyInvitationStatus.Pending &&
                invitation.ExpiresAtUtc > utcNow);
        }
        else if (status == AgencyInvitationStatus.Expired)
        {
            query = query.Where(invitation =>
                invitation.Status ==
                    AgencyInvitationStatus.Expired ||
                (
                    invitation.Status ==
                        AgencyInvitationStatus.Pending &&
                    invitation.ExpiresAtUtc <= utcNow
                ));
        }
        else if (status.HasValue)
        {
            query = query.Where(invitation =>
                invitation.Status == status.Value);
        }

        return await query
            .OrderByDescending(invitation =>
                invitation.CreatedAtUtc)
            .ToListAsync(cancellationToken);
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

            AddParameter(
                command,
                parameterName,
                parameterType,
                parameterValue);

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

    private static void AddParameter(
        DbCommand command,
        string parameterName,
        DbType parameterType,
        object parameterValue)
    {
        DbParameter parameter =
            command.CreateParameter();

        parameter.ParameterName =
            parameterName;

        parameter.DbType =
            parameterType;

        parameter.Value =
            parameterValue;

        command.Parameters.Add(parameter);
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

    private static bool
        IsPendingInvitationUniqueViolation(
            DbUpdateException exception)
    {
        return exception.InnerException
                is PostgresException postgresException &&
            postgresException.SqlState ==
                PostgresErrorCodes.UniqueViolation &&
            string.Equals(
                postgresException.ConstraintName,
                PendingInvitationUniqueIndexName,
                StringComparison.Ordinal);
    }

    private abstract class
        AgencyInvitationMutationScopeBase
        : IAsyncDisposable
    {
        private readonly IDbContextTransaction _transaction;

        private bool _committed;
        private bool _disposed;

        protected AgencyInvitationMutationScopeBase(
            RealEstateDbContext dbContext,
            IDbContextTransaction transaction)
        {
            DbContext = dbContext;
            _transaction = transaction;
        }

        protected RealEstateDbContext DbContext { get; }

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

        protected async Task
            CompleteKnownConflictAsync()
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
                    DbContext.ChangeTracker.Clear();
                    _disposed = true;
                }
            }
        }

        protected void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    GetType().Name);
            }
        }
    }

    private sealed class
        AgencyInvitationTerminalMutationScope
        : AgencyInvitationMutationScopeBase,
          IAgencyInvitationTerminalMutationScope
    {
        public AgencyInvitationTerminalMutationScope(
            RealEstateDbContext dbContext,
            IDbContextTransaction transaction,
            AgencyInvitation invitation)
            : base(dbContext, transaction)
        {
            Invitation = invitation;
        }

        public AgencyInvitation Invitation { get; }

        public async Task
            PersistTerminalTransitionAsync(
                CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            await DbContext.SaveChangesAsync(
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
                await DbContext.SaveChangesAsync(
                    cancellationToken);

                return
                    AgencyInvitationAcceptancePersistenceResult
                        .Succeeded;
            }
            catch (DbUpdateException exception)
                when (IsAgencyMembershipUniqueViolation(
                    exception))
            {
                await CompleteKnownConflictAsync();

                return
                    AgencyInvitationAcceptancePersistenceResult
                        .MembershipAlreadyExists;
            }
        }
    }

    private sealed class
        AgencyInvitationCreationScope
        : AgencyInvitationMutationScopeBase,
          IAgencyInvitationCreationScope
    {
        public AgencyInvitationCreationScope(
            RealEstateDbContext dbContext,
            IDbContextTransaction transaction,
            AgencyInvitation? pendingInvitation)
            : base(dbContext, transaction)
        {
            PendingInvitation = pendingInvitation;
        }

        public AgencyInvitation? PendingInvitation { get; }

        public async Task PersistObservedExpiryAsync(
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            await DbContext.SaveChangesAsync(
                cancellationToken);
        }

        public async Task<
            AgencyInvitationCreationPersistenceResult>
            PersistNewInvitationAsync(
                AgencyInvitation invitation,
                CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            DbContext.AgencyInvitations.Add(
                invitation);

            try
            {
                await DbContext.SaveChangesAsync(
                    cancellationToken);

                return
                    AgencyInvitationCreationPersistenceResult
                        .Succeeded;
            }
            catch (DbUpdateException exception)
                when (IsPendingInvitationUniqueViolation(
                    exception))
            {
                await CompleteKnownConflictAsync();

                return
                    AgencyInvitationCreationPersistenceResult
                        .PendingInvitationAlreadyExists;
            }
        }
    }
}
