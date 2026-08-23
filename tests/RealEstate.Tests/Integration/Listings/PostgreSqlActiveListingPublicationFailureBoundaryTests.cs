using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RealEstate.Application.Common;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Infrastructure.Persistence.Repositories;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed class PostgreSqlActiveListingPublicationFailureBoundaryTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _setupClient;

    public PostgreSqlActiveListingPublicationFailureBoundaryTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _setupClient = factory.CreateClient();
    }

    [Fact]
    public async Task Publish_WhenDatabaseTriggerRejectsPersistence_ReturnsSanitizedUnexpectedFailureAndRollsBack()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_setupClient);
        await SetUserStatusAsync(owner.UserId, UserStatus.Active);
        await ListingTestHelpers.PrepareStrongLocationPublishableDraftAsync(
            _factory,
            listingId);
        PublicationPersistenceSnapshot before =
            await ReadPersistenceSnapshotAsync(listingId);
        string connectionString = await GetConnectionStringAsync();

        await using var failureFactory =
            new TriggerFailureWebApplicationFactory(
                connectionString,
                listingId);
        using HttpClient client = failureFactory.CreateClient();
        client.AuthorizeAs(owner.AccessToken);

        try
        {
            using HttpResponseMessage response = await client.PutAsync(
                $"/api/listings/{listingId}/publish",
                content: null);
            string responseText = await response.Content.ReadAsStringAsync();
            using JsonDocument document = JsonDocument.Parse(responseText);
            JsonElement problem = document.RootElement;

            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            response.StatusCode.Should().NotBe(HttpStatusCode.Conflict);
            response.Content.Headers.ContentType?.ToString().Should()
                .Be("application/problem+json");
            problem.GetProperty("type").GetString().Should()
                .Be("urn:realestate:error:server.unexpected");
            problem.GetProperty("title").GetString().Should()
                .Be("Unexpected server error");
            problem.GetProperty("status").GetInt32().Should().Be(500);
            problem.GetProperty("detail").GetString().Should()
                .Be("An unexpected error occurred.");
            problem.GetProperty("instance").GetString().Should()
                .Be($"/api/listings/{listingId}/publish");
            problem.GetProperty("code").GetString().Should()
                .Be(ErrorCodes.ServerUnexpected);
            problem.GetProperty("code").GetString().Should()
                .NotBe(ErrorCodes.ConflictListingNotReady);

            response.Headers.TryGetValues(
                    "X-Request-ID",
                    out IEnumerable<string>? requestIds)
                .Should().BeTrue();
            string requestId = requestIds.Should().ContainSingle().Subject;
            problem.GetProperty("traceId").GetString().Should().Be(requestId);

            responseText.Should().NotContainAny(
                "23514",
                "re_assert_active_listing_publication_integrity",
                "re_guard_listing_translation_parent_mutation",
                "TR_Listings_ActivePublicationIntegrity_Update",
                "TR_ListingTranslations_ActiveFreeze_Update",
                "Active listing publication integrity violation.",
                "Active listing translations are immutable; unpublish before editing.",
                "ListingTranslations",
                "UPDATE public",
                "PostgresException");
        }
        finally
        {
            client.ClearAuthorization();
        }

        PublicationPersistenceSnapshot after =
            await ReadPersistenceSnapshotAsync(listingId);
        after.Should().BeEquivalentTo(before, options => options.WithStrictOrdering());
    }

    private async Task SetUserStatusAsync(Guid userId, UserStatus status)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE "Users"
             SET "Status" = {status.ToString()}
             WHERE "Id" = {userId}
             """);
    }

    private async Task<string> GetConnectionStringAsync()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        return dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "Integration PostgreSQL connection string is unavailable.");
    }

    private async Task<PublicationPersistenceSnapshot>
        ReadPersistenceSnapshotAsync(Guid listingId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        var root = await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Id == listingId)
            .Select(listing => new
            {
                listing.Status,
                listing.Price,
                listing.CreatedAtUtc,
                listing.ModifiedAtUtc,
                listing.CreatedByUserId,
                listing.AgencyId
            })
            .SingleAsync();
        TranslationPersistenceSnapshot[] translations = await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Id == listingId)
            .SelectMany(listing => listing.Translations)
            .OrderBy(translation => translation.LanguageCode)
            .Select(translation => new TranslationPersistenceSnapshot(
                translation.Id,
                translation.ListingId,
                translation.LanguageCode,
                translation.Title,
                translation.City,
                translation.Description))
            .ToArrayAsync();

        return new PublicationPersistenceSnapshot(
            root.Status,
            root.Price,
            root.CreatedAtUtc,
            root.ModifiedAtUtc,
            root.CreatedByUserId,
            root.AgencyId,
            translations);
    }

    private sealed class TriggerFailureWebApplicationFactory(
        string connectionString,
        Guid targetListingId)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                connectionString);
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:DefaultConnection"] =
                                connectionString
                        }));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<RealEstateDbContext>>();
                services.RemoveAll<RealEstateDbContext>();
                services.AddDbContext<RealEstateDbContext>(
                    options => options.UseNpgsql(connectionString));

                services.RemoveAll<IListingAuthoringRepository>();
                services.AddScoped<ListingAuthoringRepository>();
                services.AddScoped<IListingAuthoringRepository>(provider =>
                    new TriggerFailureListingAuthoringRepository(
                        provider.GetRequiredService<
                            ListingAuthoringRepository>(),
                        provider.GetRequiredService<RealEstateDbContext>(),
                        targetListingId));
            });
        }
    }

    private sealed class TriggerFailureListingAuthoringRepository(
        ListingAuthoringRepository inner,
        RealEstateDbContext dbContext,
        Guid targetListingId)
        : IListingAuthoringRepository
    {
        public Task<Listing?> GetByIdReadOnlyAsync(
            Guid listingId,
            CancellationToken cancellationToken)
        {
            return inner.GetByIdReadOnlyAsync(listingId, cancellationToken);
        }

        public async Task<IListingAuthoringWriteScope?> BeginWriteAsync(
            Guid listingId,
            CancellationToken cancellationToken)
        {
            IListingAuthoringWriteScope? scope = await inner.BeginWriteAsync(
                listingId,
                cancellationToken);

            return scope is null
                ? null
                : new TriggerFailureWriteScope(
                    scope,
                    dbContext,
                    listingId == targetListingId);
        }
    }

    private sealed class TriggerFailureWriteScope(
        IListingAuthoringWriteScope inner,
        RealEstateDbContext dbContext,
        bool injectFailure)
        : IListingAuthoringWriteScope
    {
        private bool _failureInjected;

        public Listing Listing => inner.Listing;

        public void AddTranslation(ListingTranslation translation) =>
            inner.AddTranslation(translation);

        public void RemoveTranslation(ListingTranslation translation) =>
            inner.RemoveTranslation(translation);

        public void MarkListingModified() => inner.MarkListingModified();

        public async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (injectFailure && !_failureInjected)
            {
                _failureInjected = true;

                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     UPDATE public."ListingTranslations"
                     SET "Description" = NULL
                     WHERE "ListingId" = {Listing.Id}
                       AND "LanguageCode" = 'en'
                     """,
                    cancellationToken);
            }

            await inner.SaveChangesAsync(cancellationToken);
        }

        public Task CommitAsync(CancellationToken cancellationToken) =>
            inner.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private sealed record PublicationPersistenceSnapshot(
        ListingStatus Status,
        decimal Price,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc,
        Guid? CreatedByUserId,
        Guid? AgencyId,
        IReadOnlyList<TranslationPersistenceSnapshot> Translations);

    private sealed record TranslationPersistenceSnapshot(
        Guid Id,
        Guid ListingId,
        string LanguageCode,
        string Title,
        string? City,
        string? Description);
}
