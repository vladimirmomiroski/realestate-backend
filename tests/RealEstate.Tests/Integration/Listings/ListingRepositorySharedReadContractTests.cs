using System.Collections.Concurrent;
using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Infrastructure.Persistence.Repositories;

namespace RealEstate.Tests.Integration.Listings;

public sealed class ListingRepositorySharedReadContractTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;

    public ListingRepositorySharedReadContractTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    [Trait("Name", "SharedListingReadContract")]
    public async Task SharedListingReadContract_CompleteLoads_WidenOnlyRootCommands()
    {
        const string currency = "EUR";
        var (sourceId, owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(
                _httpClient,
                currency: currency);
        Guid candidateId = await ListingTestHelpers.CreateListingAsAsync(
            _httpClient,
            owner,
            currency: currency);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            sourceId,
            ListingStatus.Active);
        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            candidateId,
            ListingStatus.Active);
        await ListingTestHelpers.SeedDormantSubtypeDetailsAsync(
            _factory,
            sourceId,
            CommercialType.Office,
            LandType.AgriculturalLand);

        string connectionString;
        await using (AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope())
        {
            RealEstateDbContext dbContext = scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();
            connectionString = dbContext.Database.GetConnectionString()
                ?? throw new InvalidOperationException(
                    "The integration-test connection string is unavailable.");
        }

        var capture = new QueryCommandCaptureInterceptor();
        DbContextOptions<RealEstateDbContext> options =
            new DbContextOptionsBuilder<RealEstateDbContext>()
                .UseNpgsql(connectionString)
                .AddInterceptors(capture)
                .Options;
        await using var readDbContext = new RealEstateDbContext(options);
        var repository = new ListingRepository(readDbContext);

        var filtered = await repository.GetFilteredReadOnlyAsync(
            new GetListingsQuery
            {
                Currency = currency,
                Page = 1,
                PageSize = 20
            },
            CancellationToken.None);

        filtered.Items.Should().ContainSingle(item => item.Id == sourceId)
            .Which.CommercialDetails!.CommercialType
            .Should().Be(CommercialType.Office);
        AssertCommandShape(
            capture.TakeCommands(),
            expectedCount: 4,
            expectedRootCommandIndex: 1);

        Listing? detail = await repository.GetByIdReadOnlyAsync(
            sourceId,
            CancellationToken.None);

        detail!.LandDetails!.LandType.Should().Be(LandType.AgriculturalLand);
        AssertCommandShape(
            capture.TakeCommands(),
            expectedCount: 3,
            expectedRootCommandIndex: 0,
            referencesConfinedToRoot: false);

        var mine = await repository.GetByCreatedByUserIdAsync(
            owner.UserId,
            page: 1,
            pageSize: 20,
            CancellationToken.None);

        mine.Items.Single(item => item.Id == sourceId)
            .CommercialDetails!.CommercialType.Should().Be(CommercialType.Office);
        AssertCommandShape(
            capture.TakeCommands(),
            expectedCount: 4,
            expectedRootCommandIndex: 1,
            referencesConfinedToRoot: false);

        var comparables = await repository.GetComparableListingsReadOnlyAsync(
            sourceId,
            "en",
            limit: 12,
            CancellationToken.None);

        comparables.SourceFound.Should().BeTrue();
        comparables.Items.Should().ContainSingle(item => item.Id == candidateId);
        AssertCommandShape(
            capture.TakeCommands(),
            expectedCount: 4,
            expectedRootCommandIndex: 1);
    }

    private static void AssertCommandShape(
        IReadOnlyList<string> commands,
        int expectedCount,
        int expectedRootCommandIndex,
        bool referencesConfinedToRoot = true)
    {
        commands.Should().HaveCount(expectedCount);

        string[] rootCommands = commands.Where(command =>
            command.Contains("\"ListingCommercialDetails\"", StringComparison.Ordinal) ||
            command.Contains("\"ListingLandDetails\"", StringComparison.Ordinal))
            .ToArray();

        string rootCommand = commands[expectedRootCommandIndex];

        rootCommand.Should().Contain("\"ListingApartmentDetails\"");
        rootCommand.Should().Contain("\"ListingHouseDetails\"");
        rootCommand.Should().Contain("\"ListingCommercialDetails\"");
        rootCommand.Should().Contain("\"ListingLandDetails\"");

        rootCommands.Should().HaveCount(
            referencesConfinedToRoot ? 1 : 3,
            referencesConfinedToRoot
                ? "explicit collection hydration keeps reference joins in the root command"
                : "EF split-query collection commands retain the same bounded three-command graph");

        string translationChild = commands[^2];
        string imageChild = commands[^1];
        translationChild.Should().Contain("\"ListingTranslations\"");
        imageChild.Should().Contain("\"ListingImages\"");

        if (referencesConfinedToRoot)
        {
            translationChild.Should().NotContain("\"ListingCommercialDetails\"");
            imageChild.Should().NotContain("\"ListingLandDetails\"");
        }
    }

    private sealed class QueryCommandCaptureInterceptor : DbCommandInterceptor
    {
        private readonly ConcurrentQueue<string> _commands = new();

        public IReadOnlyList<string> TakeCommands()
        {
            var commands = new List<string>();

            while (_commands.TryDequeue(out string? command))
            {
                commands.Add(command);
            }

            return commands;
        }

        public override ValueTask<InterceptionResult<DbDataReader>>
            ReaderExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<DbDataReader> result,
                CancellationToken cancellationToken = default)
        {
            _commands.Enqueue(command.CommandText);

            return base.ReaderExecutingAsync(
                command,
                eventData,
                result,
                cancellationToken);
        }
    }
}
