using System.Data;
using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Infrastructure.Persistence.Repositories;

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
    public async Task GetByIdReadOnlyAsync_LoadsCompleteAggregateWithoutTransactionOrTracking()
    {
        Guid listingId =
            await ListingTestHelpers.CreateListingAsync(_httpClient);

        await using AsyncServiceScope serviceScope =
            _factory.Services.CreateAsyncScope();
        IListingAuthoringRepository repository =
            serviceScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>();
        RealEstateDbContext dbContext =
            serviceScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        Listing? listing = await repository.GetByIdReadOnlyAsync(
            listingId,
            CancellationToken.None);

        listing.Should().NotBeNull();
        listing!.Translations.Should().HaveCount(2);
        listing.Images.Should().BeEmpty();
        listing.ApartmentDetails.Should().NotBeNull();
        listing.HouseDetails.Should().BeNull();
        dbContext.Database.CurrentTransaction.Should().BeNull();
        dbContext.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdReadOnlyAsync_WithMultipleCollections_UsesExactlyThreeCommands()
    {
        Guid listingId =
            await ListingTestHelpers.CreateListingAsync(_httpClient);

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            listingId,
            CreateTranslation("sq", "Titull"),
            CreateTranslation("de", "Titel"),
            CreateTranslation("mk", "Наслов"),
            CreateTranslation("en", "Title"));

        Guid firstImageId = Guid.NewGuid();
        Guid secondImageId = Guid.NewGuid();
        Guid thirdImageId = Guid.NewGuid();
        string connectionString;

        await using (AsyncServiceScope seedScope =
            _factory.Services.CreateAsyncScope())
        {
            RealEstateDbContext seedDbContext =
                seedScope.ServiceProvider
                    .GetRequiredService<RealEstateDbContext>();

            seedDbContext.Set<ListingImage>().AddRange(
                CreateImage(firstImageId, listingId, sortOrder: 2),
                CreateImage(secondImageId, listingId, sortOrder: 0, isPrimary: true),
                CreateImage(thirdImageId, listingId, sortOrder: 1));

            await seedDbContext.SaveChangesAsync();

            connectionString = seedDbContext.Database.GetConnectionString()
                ?? throw new InvalidOperationException(
                    "The integration-test connection string is unavailable.");
        }

        var commandCapture = new QueryCommandCaptureInterceptor();
        DbContextOptions<RealEstateDbContext> options =
            new DbContextOptionsBuilder<RealEstateDbContext>()
                .UseNpgsql(connectionString)
                .AddInterceptors(commandCapture)
                .Options;

        await using var readDbContext = new RealEstateDbContext(options);
        var repository = new ListingAuthoringRepository(readDbContext);

        Listing? listing = await repository.GetByIdReadOnlyAsync(
            listingId,
            CancellationToken.None);

        listing.Should().NotBeNull();
        listing!.Translations.Should().HaveCount(4);
        listing.Images.Should().HaveCount(3);
        listing.ApartmentDetails.Should().NotBeNull();
        listing.HouseDetails.Should().BeNull();

        var response = listing.ToAuthoringResponse();
        response.Translations.Select(translation => translation.LanguageCode)
            .Should().Equal("de", "en", "mk", "sq");
        response.Images.Select(image => image.Id)
            .Should().Equal(secondImageId, thirdImageId, firstImageId);

        IReadOnlyList<string> commands = commandCapture.Commands;
        commands.Should().HaveCount(
            3,
            "the split query is one root/detail command plus one command per collection");
        commands.Should().ContainSingle(command =>
            command.Contains("\"ListingTranslations\"", StringComparison.Ordinal));
        commands.Should().ContainSingle(command =>
            command.Contains("\"ListingImages\"", StringComparison.Ordinal));
        commands.Should().ContainSingle(command =>
            !command.Contains("\"ListingTranslations\"", StringComparison.Ordinal) &&
            !command.Contains("\"ListingImages\"", StringComparison.Ordinal) &&
            command.Contains("\"ListingApartmentDetails\"", StringComparison.Ordinal) &&
            command.Contains("\"ListingHouseDetails\"", StringComparison.Ordinal));
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
        await ListingTestHelpers.PrepareStrongLocationPublishableDraftAsync(
            _factory,
            listingId);

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
        await ListingTestHelpers.PrepareStrongLocationPublishableDraftAsync(
            _factory,
            listingId);

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

    private static ListingTranslation CreateTranslation(
        string languageCode,
        string title)
    {
        return new ListingTranslation
        {
            Id = Guid.NewGuid(),
            LanguageCode = languageCode,
            Title = title,
            Description = $"{title} description",
            City = "Skopje"
        };
    }

    private static ListingImage CreateImage(
        Guid imageId,
        Guid listingId,
        int sortOrder,
        bool isPrimary = false)
    {
        return new ListingImage
        {
            Id = imageId,
            ListingId = listingId,
            OriginalFileName = $"image-{sortOrder}.jpg",
            StoredFileName = $"{imageId:N}.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 1000 + sortOrder,
            Url = $"/uploads/listings/{imageId:N}.jpg",
            SortOrder = sortOrder,
            IsPrimary = isPrimary
        };
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

    private sealed class QueryCommandCaptureInterceptor
        : DbCommandInterceptor
    {
        private readonly List<string> _commands = [];

        public IReadOnlyList<string> Commands => _commands;

        public override ValueTask<InterceptionResult<DbDataReader>>
            ReaderExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<DbDataReader> result,
                CancellationToken cancellationToken = default)
        {
            _commands.Add(command.CommandText);

            return base.ReaderExecutingAsync(
                command,
                eventData,
                result,
                cancellationToken);
        }
    }
}
