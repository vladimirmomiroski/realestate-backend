using System.Data;
using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;

namespace RealEstate.Tests.Integration.Listings;

public sealed class ListingAuthoringRepositoryTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;

    public ListingAuthoringRepositoryTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task BeginWriteAsync_WhenListingIsMissing_CleansUpTransaction()
    {
        await using AsyncServiceScope serviceScope =
            _factory.Services.CreateAsyncScope();

        IListingAuthoringRepository repository =
            serviceScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>();

        RealEstateDbContext dbContext =
            serviceScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        IListingAuthoringWriteScope? writeScope =
            await repository.BeginWriteAsync(
                Guid.NewGuid(),
                CancellationToken.None);

        writeScope.Should().BeNull();
        dbContext.Database.CurrentTransaction.Should().BeNull();
    }

    [Fact]
    public async Task DisposeAsync_WithoutCommit_RollsBackSavedMutation()
    {
        Guid listingId =
            await ListingTestHelpers.CreateListingAsync(_httpClient);

        await using (AsyncServiceScope serviceScope =
            _factory.Services.CreateAsyncScope())
        {
            IListingAuthoringRepository repository =
                serviceScope.ServiceProvider
                    .GetRequiredService<IListingAuthoringRepository>();

            IListingAuthoringWriteScope? writeScope =
                await repository.BeginWriteAsync(
                    listingId,
                    CancellationToken.None);

            writeScope.Should().NotBeNull();

            await using (writeScope!)
            {
                writeScope.Listing.Publish();

                await writeScope.SaveChangesAsync(
                    CancellationToken.None);
            }
        }

        ListingStatus persistedStatus =
            await GetListingStatusAsync(listingId);

        persistedStatus.Should().Be(ListingStatus.Draft);
    }

    [Fact]
    public async Task CompetingStatusWrites_SerializeOnParentListingLock()
    {
        Guid listingId =
            await ListingTestHelpers.CreateListingAsync(_httpClient);

        await using AsyncServiceScope firstServiceScope =
            _factory.Services.CreateAsyncScope();
        await using AsyncServiceScope secondServiceScope =
            _factory.Services.CreateAsyncScope();

        IListingAuthoringRepository firstRepository =
            firstServiceScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>();
        IListingAuthoringRepository secondRepository =
            secondServiceScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>();

        RealEstateDbContext secondDbContext =
            secondServiceScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        int secondBackendProcessId =
            await GetBackendProcessIdAsync(secondDbContext);

        IListingAuthoringWriteScope? firstWriteScope =
            await firstRepository.BeginWriteAsync(
                listingId,
                CancellationToken.None);

        firstWriteScope.Should().NotBeNull();

        IListingAuthoringWriteScope? secondWriteScope = null;
        Task<IListingAuthoringWriteScope?>? secondBeginTask = null;
        bool lockWaitObserved = false;
        ListingStatus? secondObservedStatus = null;

        try
        {
            firstWriteScope!.Listing.Publish();

            await firstWriteScope.SaveChangesAsync(
                CancellationToken.None);

            secondBeginTask = secondRepository.BeginWriteAsync(
                listingId,
                CancellationToken.None);

            lockWaitObserved = await WaitForLockWaitAsync(
                secondBackendProcessId,
                TimeSpan.FromSeconds(10));

            await firstWriteScope.CommitAsync(
                CancellationToken.None);

            secondWriteScope = await secondBeginTask.WaitAsync(
                TimeSpan.FromSeconds(10));

            if (secondWriteScope is not null)
            {
                secondObservedStatus = secondWriteScope.Listing.Status;
                secondWriteScope.Listing.Archive();

                await secondWriteScope.SaveChangesAsync(
                    CancellationToken.None);
                await secondWriteScope.CommitAsync(
                    CancellationToken.None);
            }
        }
        finally
        {
            try
            {
                await firstWriteScope!.DisposeAsync();
            }
            finally
            {
                try
                {
                    if (secondBeginTask is not null)
                    {
                        secondWriteScope ??=
                            await secondBeginTask.WaitAsync(
                                TimeSpan.FromSeconds(10));
                    }
                }
                finally
                {
                    if (secondWriteScope is not null)
                    {
                        await secondWriteScope.DisposeAsync();
                    }
                }
            }
        }

        lockWaitObserved.Should().BeTrue();
        secondWriteScope.Should().NotBeNull();
        secondObservedStatus.Should().Be(ListingStatus.Active);

        ListingStatus persistedStatus =
            await GetListingStatusAsync(listingId);

        persistedStatus.Should().Be(ListingStatus.Archived);
    }

    private async Task<ListingStatus> GetListingStatusAsync(
        Guid listingId)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        RealEstateDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        return await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Id == listingId)
            .Select(listing => listing.Status)
            .SingleAsync();
    }

    private static async Task<int> GetBackendProcessIdAsync(
        RealEstateDbContext dbContext)
    {
        await dbContext.Database.OpenConnectionAsync();

        await using DbCommand command =
            dbContext.Database.GetDbConnection().CreateCommand();

        command.CommandText = "SELECT pg_backend_pid();";

        object? result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result);
    }

    private async Task<bool> WaitForLockWaitAsync(
        int backendProcessId,
        TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            await using AsyncServiceScope scope =
                _factory.Services.CreateAsyncScope();

            RealEstateDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

            await dbContext.Database.OpenConnectionAsync();

            await using DbCommand command =
                dbContext.Database.GetDbConnection().CreateCommand();

            command.CommandText =
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_stat_activity
                    WHERE pid = @backendProcessId
                      AND wait_event_type = 'Lock'
                      AND query ILIKE '%FOR UPDATE%'
                );
                """;

            DbParameter parameter =
                command.CreateParameter();

            parameter.ParameterName =
                "@backendProcessId";
            parameter.DbType =
                DbType.Int32;
            parameter.Value =
                backendProcessId;

            command.Parameters.Add(parameter);

            if (await command.ExecuteScalarAsync() is true)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25));
        }

        return false;
    }
}
