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
using Microsoft.Extensions.Logging;
using Npgsql;
using RealEstate.Api.Errors;
using RealEstate.Application.Common;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Infrastructure.Persistence.Repositories;
using RealEstate.Tests.Integration.Api;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed class PostgreSqlActiveListingPublicationFailureBoundaryTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string CompletionCategory =
        "RealEstate.Api.Errors.ApiRequestCompletionLoggingMiddleware";
    private const string ExceptionCategory =
        "RealEstate.Api.Errors.ApiExceptionHandler";
    private const string IntegrityViolationMessage =
        "Active listing publication integrity violation.";

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
        var logs = new CapturingLoggerProvider();

        await using var failureFactory =
            new TriggerFailureWebApplicationFactory(
                connectionString,
                listingId,
                logs);
        using HttpClient client = failureFactory.CreateClient();
        client.AuthorizeAs(owner.AccessToken);
        logs.Clear();

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
                "PostgresException",
                "InvalidMunicipality",
                "listing_not_ready");
            responseText.Should().NotContain(before.ProviderKey);
            responseText.Should().NotContain(before.ResultReference);

            CapturedLogEntry completion = logs.Entries
                .Where(entry =>
                    entry.Category == CompletionCategory &&
                    entry.EventId ==
                        ApiRequestCompletionLoggingMiddleware.CompletionEvent)
                .Should().ContainSingle().Subject;
            CapturedLogEntry error = logs.Entries
                .Where(entry =>
                    entry.Category == ExceptionCategory &&
                    entry.EventId == ApiExceptionHandler.HandledExceptionEvent)
                .Should().ContainSingle().Subject;

            completion.Properties["StatusCode"].Should().Be(500);
            completion.Properties["RequestId"].Should().Be(requestId);
            error.Level.Should().Be(LogLevel.Error);
            error.Properties["RequestId"].Should().Be(requestId);
            error.Properties["Method"].Should().Be("PUT");
            error.Properties["Route"].Should()
                .Be("api/listings/{id:guid}/publish");
            error.Properties["StatusCode"].Should().Be(500);
            error.Properties.Keys.Should().BeEquivalentTo(
                "RequestId",
                "Method",
                "Route",
                "StatusCode",
                "{OriginalFormat}");
            error.ScopeProperties["RequestId"].Should().Be(requestId);

            error.Exception.Should().NotBeNull();
            PostgresException postgresException = FindPostgresException(
                error.Exception!);
            postgresException.SqlState.Should().Be(
                PostgresErrorCodes.CheckViolation);
            postgresException.ConstraintName.Should().BeNull();
            postgresException.MessageText.Should()
                .Be(IntegrityViolationMessage);
            logs.Entries.Should().NotContain(entry =>
                entry.Category ==
                    "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware" &&
                entry.EventId.Id == 1);
            AssertCustomLogsExclude(
                logs,
                before.ProviderKey,
                before.ResultReference);
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
                listing.AgencyId,
                listing.Latitude,
                listing.Longitude,
                listing.LocationPrecision,
                listing.GeocodingProviderKey,
                listing.GeocodingResultReference,
                listing.GeocodedDisplayName,
                listing.LocationConfirmedAtUtc
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
                translation.Municipality,
                translation.AddressLine,
                translation.Description))
            .ToArrayAsync();

        return new PublicationPersistenceSnapshot(
            root.Status,
            root.Price,
            root.CreatedAtUtc,
            root.ModifiedAtUtc,
            root.CreatedByUserId,
            root.AgencyId,
            root.Latitude,
            root.Longitude,
            root.LocationPrecision,
            root.GeocodingProviderKey!,
            root.GeocodingResultReference!,
            root.GeocodedDisplayName,
            root.LocationConfirmedAtUtc,
            translations);
    }

    private static PostgresException FindPostgresException(
        Exception exception)
    {
        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            if (current is PostgresException postgresException)
            {
                return postgresException;
            }
        }

        throw new InvalidOperationException(
            "The logged persistence failure did not contain PostgreSQL diagnostics.");
    }

    private static void AssertCustomLogsExclude(
        CapturingLoggerProvider logs,
        params string[] sensitiveValues)
    {
        CapturedLogEntry[] customEntries = logs.Entries
            .Where(entry =>
                entry.Category == CompletionCategory ||
                entry.Category == ExceptionCategory)
            .ToArray();

        foreach (string sensitiveValue in sensitiveValues)
        {
            customEntries.Should().NotContain(entry =>
                entry.Message.Contains(
                    sensitiveValue,
                    StringComparison.Ordinal) ||
                entry.Properties.Values.Any(value =>
                    ContainsSensitiveValue(value, sensitiveValue)) ||
                entry.ScopeProperties.Values.Any(value =>
                    ContainsSensitiveValue(value, sensitiveValue)) ||
                ContainsSensitiveValue(entry.Exception, sensitiveValue));
        }
    }

    private static bool ContainsSensitiveValue(
        object? value,
        string sensitiveValue)
    {
        return value?.ToString()?.Contains(
            sensitiveValue,
            StringComparison.Ordinal) == true;
    }

    private sealed class TriggerFailureWebApplicationFactory(
        string connectionString,
        Guid targetListingId,
        CapturingLoggerProvider logs)
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
            builder.ConfigureLogging(logging =>
                logging.AddProvider(logs));

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
                     SET "Municipality" = NULL
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
        decimal? Latitude,
        decimal? Longitude,
        LocationPrecision? Precision,
        string ProviderKey,
        string ResultReference,
        string? DisplayName,
        DateTime? ConfirmedAtUtc,
        IReadOnlyList<TranslationPersistenceSnapshot> Translations);

    private sealed record TranslationPersistenceSnapshot(
        Guid Id,
        Guid ListingId,
        string LanguageCode,
        string Title,
        string? City,
        string? Municipality,
        string? AddressLine,
        string? Description);
}
