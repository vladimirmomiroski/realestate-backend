using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Entities;

namespace RealEstate.Infrastructure.Persistence.Repositories;

public sealed class ListingAuthoringRepository
    : IListingAuthoringRepository
{
    private readonly RealEstateDbContext _dbContext;

    public ListingAuthoringRepository(
        RealEstateDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IListingAuthoringWriteScope?> BeginWriteAsync(
        Guid listingId,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        bool transactionOwnedByMethod = true;

        try
        {
            bool listingExists = await LockListingAsync(
                listingId,
                transaction,
                cancellationToken);

            if (!listingExists)
            {
                transactionOwnedByMethod = false;

                await RollbackAndDisposeAsync(transaction);

                return null;
            }

            Listing? listing = await _dbContext.Listings
                .Include(currentListing => currentListing.Translations)
                .Include(currentListing => currentListing.Images)
                .Include(currentListing => currentListing.ApartmentDetails)
                .Include(currentListing => currentListing.HouseDetails)
                .AsSplitQuery()
                .SingleOrDefaultAsync(
                    currentListing => currentListing.Id == listingId,
                    cancellationToken);

            if (listing is null)
            {
                transactionOwnedByMethod = false;

                await RollbackAndDisposeAsync(transaction);

                return null;
            }

            var writeScope = new ListingAuthoringWriteScope(
                listing,
                _dbContext,
                transaction);

            transactionOwnedByMethod = false;

            return writeScope;
        }
        catch
        {
            if (transactionOwnedByMethod)
            {
                await RollbackAndDisposeAsync(transaction);
            }

            throw;
        }
    }

    private async Task<bool> LockListingAsync(
        Guid listingId,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
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
            FROM "Listings"
            WHERE "Id" = @listingId
            FOR UPDATE;
            """;

        DbParameter listingIdParameter =
            command.CreateParameter();

        listingIdParameter.ParameterName =
            "@listingId";

        listingIdParameter.DbType =
            DbType.Guid;

        listingIdParameter.Value =
            listingId;

        command.Parameters.Add(listingIdParameter);

        object? lockedListingId =
            await command.ExecuteScalarAsync(cancellationToken);

        return lockedListingId is not null &&
               lockedListingId is not DBNull;
    }

    private static async Task RollbackAndDisposeAsync(
        IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        finally
        {
            await transaction.DisposeAsync();
        }
    }

    private sealed class ListingAuthoringWriteScope
        : IListingAuthoringWriteScope
    {
        private readonly RealEstateDbContext _dbContext;
        private readonly IDbContextTransaction _transaction;

        private bool _committed;
        private bool _disposed;

        public ListingAuthoringWriteScope(
            Listing listing,
            RealEstateDbContext dbContext,
            IDbContextTransaction transaction)
        {
            Listing = listing;
            _dbContext = dbContext;
            _transaction = transaction;
        }

        public Listing Listing { get; }

        public async Task SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task CommitAsync(
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_committed)
            {
                return;
            }

            await _transaction.CommitAsync(cancellationToken);

            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

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
                await _transaction.DisposeAsync();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(ListingAuthoringWriteScope));
            }
        }
    }
}
