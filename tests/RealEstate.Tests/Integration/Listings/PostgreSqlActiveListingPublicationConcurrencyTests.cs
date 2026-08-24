using System.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RealEstate.Infrastructure.Persistence;

namespace RealEstate.Tests.Integration.Listings;

public sealed class PostgreSqlActiveListingPublicationConcurrencyTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string ActiveFreezeMessage =
        "Active listing translations are immutable; unpublish before editing.";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(15);

    private readonly CustomWebApplicationFactory _factory;

    public PostgreSqlActiveListingPublicationConcurrencyTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RepeatableReadStaleActivation_AfterCommittedDraftChildMutation_IsRejected()
    {
        Guid listingId = Guid.NewGuid();
        Guid translationId = Guid.NewGuid();
        string connectionString = await GetPinnedConnectionStringAsync();

        await using var setupConnection = new NpgsqlConnection(connectionString);
        await setupConnection.OpenAsync();
        await SeedReadyDraftAsync(setupConnection, listingId, translationId);
        ListingState initialState = await ReadStateAsync(
            setupConnection,
            transaction: null,
            listingId,
            translationId);

        initialState.Status.Should().Be("Draft");
        initialState.City.Should().Be("Skopje");
        initialState.Description.Should().Be("Ready description");

        await using var activationConnection =
            new NpgsqlConnection(connectionString);
        await using var mutationConnection =
            new NpgsqlConnection(connectionString);
        await using var observerConnection =
            new NpgsqlConnection(connectionString);
        await Task.WhenAll(
            activationConnection.OpenAsync(),
            mutationConnection.OpenAsync(),
            observerConnection.OpenAsync());

        int activationBackendPid =
            await ReadBackendPidAsync(activationConnection);
        int mutationBackendPid = await ReadBackendPidAsync(mutationConnection);
        int observerBackendPid = await ReadBackendPidAsync(observerConnection);

        new[] { activationBackendPid, mutationBackendPid, observerBackendPid }
            .Distinct()
            .Should().HaveCount(3);

        await using NpgsqlTransaction activationTransaction =
            await activationConnection.BeginTransactionAsync(
                IsolationLevel.RepeatableRead);
        await using NpgsqlTransaction mutationTransaction =
            await mutationConnection.BeginTransactionAsync(
                IsolationLevel.ReadCommitted);

        bool mutationCommitted = false;
        bool activationRolledBack = false;
        PostgresException? activationException = null;
        ListingState? staleState = null;
        ListingState? committedState = null;

        try
        {
            ListingState activationSnapshot = await ReadStateAsync(
                activationConnection,
                activationTransaction,
                listingId,
                translationId);

            activationSnapshot.Status.Should().Be("Draft");
            activationSnapshot.City.Should().Be("Skopje");
            activationSnapshot.Description.Should().Be("Ready description");
            activationSnapshot.Xmin.Should().Be(initialState.Xmin);

            await SetTranslationCityAsync(
                mutationConnection,
                mutationTransaction,
                translationId,
                city: null);
            await mutationTransaction.CommitAsync();
            mutationCommitted = true;

            committedState = await ReadStateAsync(
                observerConnection,
                transaction: null,
                listingId,
                translationId);
            committedState.Status.Should().Be("Draft");
            committedState.City.Should().BeNull();
            committedState.Xmin.Should().NotBe(initialState.Xmin);

            staleState = await ReadStateAsync(
                activationConnection,
                activationTransaction,
                listingId,
                translationId);
            staleState.Status.Should().Be("Draft");
            staleState.City.Should().Be("Skopje");
            staleState.Xmin.Should().Be(initialState.Xmin);

            activationException = await Assert.ThrowsAsync<PostgresException>(
                () => ActivateListingAsync(
                    activationConnection,
                    activationTransaction,
                    listingId));
            await activationTransaction.RollbackAsync();
            activationRolledBack = true;
        }
        finally
        {
            if (!mutationCommitted)
            {
                await RollbackSafelyAsync(mutationTransaction);
            }

            if (!activationRolledBack)
            {
                await RollbackSafelyAsync(activationTransaction);
            }
        }

        activationException.Should().NotBeNull();
        activationException!.SqlState.Should()
            .Be(PostgresErrorCodes.SerializationFailure);
        staleState.Should().NotBeNull();
        committedState.Should().NotBeNull();

        ListingState finalState = await ReadStateAsync(
            observerConnection,
            transaction: null,
            listingId,
            translationId);
        finalState.Status.Should().Be("Draft");
        finalState.City.Should().BeNull();
        finalState.Description.Should().Be("Ready description");
        finalState.Xmin.Should().Be(committedState!.Xmin);
    }

    [Fact]
    public async Task ActivationFirst_BlocksThenRejectsDraftChildMutation()
    {
        Guid listingId = Guid.NewGuid();
        Guid translationId = Guid.NewGuid();
        string connectionString = await GetPinnedConnectionStringAsync();

        await using var setupConnection = new NpgsqlConnection(connectionString);
        await setupConnection.OpenAsync();
        await SeedReadyDraftAsync(setupConnection, listingId, translationId);
        ListingState initialState = await ReadStateAsync(
            setupConnection,
            transaction: null,
            listingId,
            translationId);

        await using var activationConnection =
            new NpgsqlConnection(connectionString);
        await using var mutationConnection =
            new NpgsqlConnection(connectionString);
        await using var observerConnection =
            new NpgsqlConnection(connectionString);
        await Task.WhenAll(
            activationConnection.OpenAsync(),
            mutationConnection.OpenAsync(),
            observerConnection.OpenAsync());

        int activationBackendPid =
            await ReadBackendPidAsync(activationConnection);
        int mutationBackendPid = await ReadBackendPidAsync(mutationConnection);
        int observerBackendPid = await ReadBackendPidAsync(observerConnection);

        new[] { activationBackendPid, mutationBackendPid, observerBackendPid }
            .Distinct()
            .Should().HaveCount(3);

        await using NpgsqlTransaction activationTransaction =
            await activationConnection.BeginTransactionAsync(
                IsolationLevel.ReadCommitted);
        await using NpgsqlTransaction mutationTransaction =
            await mutationConnection.BeginTransactionAsync(
                IsolationLevel.ReadCommitted);

        Task? mutationTask = null;
        bool activationCommitted = false;
        bool mutationRolledBack = false;
        PostgresException? mutationException = null;

        try
        {
            await ActivateListingAsync(
                activationConnection,
                activationTransaction,
                listingId);

            mutationTask = SetTranslationCityAsync(
                mutationConnection,
                mutationTransaction,
                translationId,
                city: null);

            using var contentionTimeout =
                new CancellationTokenSource(TestTimeout);
            await WaitForBlockedTranslationMutationAsync(
                observerConnection,
                mutationBackendPid,
                activationBackendPid,
                mutationTask,
                contentionTimeout.Token);

            mutationTask.IsCompleted.Should().BeFalse();

            await activationTransaction.CommitAsync();
            activationCommitted = true;

            mutationException = await Assert.ThrowsAsync<PostgresException>(
                async () => await mutationTask);
            await mutationTransaction.RollbackAsync();
            mutationRolledBack = true;
        }
        finally
        {
            if (!activationCommitted)
            {
                await RollbackSafelyAsync(activationTransaction);
            }

            if (mutationTask is not null)
            {
                try
                {
                    await mutationTask.WaitAsync(TestTimeout);
                }
                catch
                {
                    // The assertion below owns the expected database failure;
                    // cleanup only guarantees the task is observed and drained.
                }
            }

            if (!mutationRolledBack)
            {
                await RollbackSafelyAsync(mutationTransaction);
            }
        }

        mutationException.Should().NotBeNull();
        mutationException!.SqlState.Should()
            .Be(PostgresErrorCodes.CheckViolation);
        mutationException.MessageText.Should().Be(ActiveFreezeMessage);

        ListingState finalState = await ReadStateAsync(
            observerConnection,
            transaction: null,
            listingId,
            translationId);
        finalState.Status.Should().Be("Active");
        finalState.City.Should().Be("Skopje");
        finalState.Description.Should().Be("Ready description");
        finalState.Xmin.Should().NotBe(initialState.Xmin);
    }

    private async Task<string> GetPinnedConnectionStringAsync()
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        string connectionString = dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "The PostgreSQL connection string is unavailable.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
            ApplicationName = nameof(
                PostgreSqlActiveListingPublicationConcurrencyTests)
        };
        return builder.ConnectionString;
    }

    private static async Task SeedReadyDraftAsync(
        NpgsqlConnection connection,
        Guid listingId,
        Guid translationId)
    {
        await using NpgsqlCommand listingCommand = connection.CreateCommand();
        listingCommand.CommandText =
            """
            INSERT INTO public."Listings"
                ("Id", "ListingType", "PropertyType", "Status", "Price",
                 "Currency", "AreaSquareMeters", "Latitude", "Longitude",
                 "LocationPrecision", "GeocodingProviderKey",
                 "GeocodingResultReference", "LocationConfirmedAtUtc",
                 "CreatedAtUtc")
            VALUES
                (@listingId, 'Sale', 'Apartment', 'Draft', 100000,
                 'EUR', 80, 41.9981, 21.4254, 'ExactAddress',
                 'integrity-concurrency-test',
                 'integrity-concurrency-reference',
                 TIMESTAMPTZ '2026-08-24 12:00:00+00', @createdAtUtc);
            """;
        listingCommand.Parameters.AddWithValue("listingId", listingId);
        listingCommand.Parameters.AddWithValue("createdAtUtc", DateTime.UtcNow);
        await listingCommand.ExecuteNonQueryAsync();

        await using NpgsqlCommand translationCommand =
            connection.CreateCommand();
        translationCommand.CommandText =
            """
            INSERT INTO public."ListingTranslations"
                ("Id", "ListingId", "LanguageCode", "Title", "City",
                 "Municipality", "AddressLine", "Description")
            VALUES
                (@translationId, @listingId, 'en', 'Ready title', 'Skopje',
                 'Centar', 'Concurrency address', 'Ready description');
            """;
        translationCommand.Parameters.AddWithValue(
            "translationId",
            translationId);
        translationCommand.Parameters.AddWithValue("listingId", listingId);
        await translationCommand.ExecuteNonQueryAsync();
    }

    private static async Task<ListingState> ReadStateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid listingId,
        Guid translationId)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT listing."Status",
                   translation."City",
                   translation."Description",
                   listing.xmin::text::bigint
            FROM public."Listings" AS listing
            INNER JOIN public."ListingTranslations" AS translation
                ON translation."ListingId" = listing."Id"
            WHERE listing."Id" = @listingId
              AND translation."Id" = @translationId;
            """;
        command.Parameters.AddWithValue("listingId", listingId);
        command.Parameters.AddWithValue("translationId", translationId);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException(
                "The seeded listing aggregate was not found.");
        }

        return new ListingState(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetInt64(3));
    }

    private static async Task<int> ReadBackendPidAsync(
        NpgsqlConnection connection)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT pg_catalog.pg_backend_pid();";
        object result = await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException(
                "PostgreSQL did not return a backend PID.");
        return Convert.ToInt32(result);
    }

    private static async Task ActivateListingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid listingId)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE public."Listings"
            SET "Status" = 'Active'
            WHERE "Id" = @listingId;
            """;
        command.Parameters.AddWithValue("listingId", listingId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SetTranslationCityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid translationId,
        string? city)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE public."ListingTranslations"
            SET "City" = @city
            WHERE "Id" = @translationId;
            """;
        command.Parameters.AddWithValue("translationId", translationId);
        command.Parameters.AddWithValue(
            "city",
            city is null ? DBNull.Value : city);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task WaitForBlockedTranslationMutationAsync(
        NpgsqlConnection observerConnection,
        int waitingBackendPid,
        int blockingBackendPid,
        Task competingTask,
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                if (competingTask.IsCompleted)
                {
                    throw new InvalidOperationException(
                        "The translation mutation completed before PostgreSQL " +
                        "reported parent-row lock contention.");
                }

                await using NpgsqlCommand command =
                    observerConnection.CreateCommand();
                command.CommandTimeout = 2;
                command.CommandText =
                    """
                    SELECT EXISTS (
                        SELECT 1
                        FROM pg_catalog.pg_stat_activity AS activity
                        WHERE activity.datname = pg_catalog.current_database()
                          AND activity.pid = @waitingBackendPid
                          AND activity.state = 'active'
                          AND activity.wait_event_type = 'Lock'
                          AND activity.query ILIKE
                              '%UPDATE public."ListingTranslations"%'
                          AND @blockingBackendPid = ANY(
                              pg_catalog.pg_blocking_pids(activity.pid))
                    );
                    """;
                command.Parameters.AddWithValue(
                    "waitingBackendPid",
                    waitingBackendPid);
                command.Parameters.AddWithValue(
                    "blockingBackendPid",
                    blockingBackendPid);

                object? result = await command.ExecuteScalarAsync(
                    cancellationToken);
                if (result is true)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException exception)
            when (cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                "PostgreSQL did not report the translation mutation blocked " +
                "on the expected parent-row lock.",
                exception);
        }
    }

    private static async Task RollbackSafelyAsync(
        NpgsqlTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync();
        }
        catch (InvalidOperationException)
        {
            // The transaction was already completed or its connection closed.
        }
        catch (PostgresException)
        {
            // A failed PostgreSQL statement can leave a transaction aborted;
            // disposal remains the final cleanup safeguard.
        }
    }

    private sealed record ListingState(
        string Status,
        string? City,
        string? Description,
        long Xmin);
}
