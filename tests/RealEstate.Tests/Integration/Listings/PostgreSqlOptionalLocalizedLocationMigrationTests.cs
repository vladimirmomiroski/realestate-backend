using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Infrastructure.Persistence.Configurations;

namespace RealEstate.Tests.Integration.Listings;

public sealed class PostgreSqlOptionalLocalizedLocationMigrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string PreviousMigration =
        "20260811091318_EnforceActiveListingPublicationIntegrity";
    private const string CurrentMigration =
        "20260812172728_EnforceOptionalLocalizedLocationRowIntegrity";

    private static readonly IReadOnlyDictionary<string, string>
        ConstraintColumns = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ListingTranslationConfiguration.AddressLineConstraintName] =
                "AddressLine",
            [ListingTranslationConfiguration.MunicipalityConstraintName] =
                "Municipality",
            [ListingTranslationConfiguration.NeighborhoodConstraintName] =
                "Neighborhood"
        };

    private readonly CustomWebApplicationFactory _factory;

    public PostgreSqlOptionalLocalizedLocationMigrationTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Catalog_ContainsNamedUnicodeConstraintsOnNullableColumns()
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        IReadOnlyDictionary<string, string> constraints =
            await ReadOwnedConstraintsAsync(dbContext);
        IReadOnlyDictionary<string, bool> nullability =
            await ReadColumnNullabilityAsync(dbContext);

        constraints.Keys.Should().BeEquivalentTo(ConstraintColumns.Keys);

        foreach ((string constraintName, string columnName) in ConstraintColumns)
        {
            constraints[constraintName].Should().ContainAll(
                $"\"{columnName}\"",
                "IS NULL",
                "<> ''::text",
                "btrim",
                "chr(160)",
                "chr(8195)",
                "chr(12288)");
            nullability[columnName].Should().BeTrue();
        }
    }

    [Fact]
    public async Task MigrationLifecycle_FreshRepeatDownAndUpAgain_IsExact()
    {
        await using IsolatedMigrationDatabase database =
            await CreateIsolatedMigrationDatabaseAsync();
        await using RealEstateDbContext dbContext = database.CreateContext();
        IMigrator migrator = dbContext.GetService<IMigrator>();

        await migrator.MigrateAsync(CurrentMigration);
        (await ReadOwnedConstraintsAsync(dbContext)).Should().HaveCount(3);
        (await ReadMigrationAppliedAsync(dbContext, CurrentMigration))
            .Should().BeTrue();

        await migrator.MigrateAsync(CurrentMigration);
        (await ReadOwnedConstraintsAsync(dbContext)).Should().HaveCount(3);

        await migrator.MigrateAsync(PreviousMigration);
        (await ReadOwnedConstraintsAsync(dbContext)).Should().BeEmpty();
        (await CountEarlierRowConstraintsAsync(dbContext)).Should().Be(4);
        (await ReadColumnNullabilityAsync(dbContext)).Values
            .Should().OnlyContain(isNullable => isNullable);
        (await ReadMigrationAppliedAsync(dbContext, CurrentMigration))
            .Should().BeFalse();

        await migrator.MigrateAsync(CurrentMigration);
        (await ReadOwnedConstraintsAsync(dbContext)).Should().HaveCount(3);
        (await CountEarlierRowConstraintsAsync(dbContext)).Should().Be(4);
    }

    [Fact]
    public async Task MigrationWithMalformedExistingValue_FailsWithoutRepair()
    {
        await using IsolatedMigrationDatabase database =
            await CreateIsolatedMigrationDatabaseAsync();
        Guid listingId = Guid.NewGuid();
        Guid translationId = Guid.NewGuid();

        await using (RealEstateDbContext setupContext = database.CreateContext())
        {
            IMigrator migrator = setupContext.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            await InsertDraftListingAndTranslationAsync(
                setupContext,
                listingId,
                translationId,
                neighborhood: " ");
        }

        await using (RealEstateDbContext migrationContext = database.CreateContext())
        {
            IMigrator migrator = migrationContext.GetService<IMigrator>();
            Func<Task> action = () => migrator.MigrateAsync(CurrentMigration);

            PostgresException exception =
                (await action.Should().ThrowAsync<PostgresException>()).Which;
            exception.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
            exception.ConstraintName.Should().Be(
                ListingTranslationConfiguration.NeighborhoodConstraintName);
        }

        await using (RealEstateDbContext verificationContext = database.CreateContext())
        {
            (await ReadOwnedConstraintsAsync(verificationContext))
                .Should().BeEmpty();
            (await CountEarlierRowConstraintsAsync(verificationContext))
                .Should().Be(4);
            IReadOnlyDictionary<string, bool> nullability =
                await ReadColumnNullabilityAsync(verificationContext);
            nullability.Should().HaveCount(3);
            nullability.Values
                .Should().OnlyContain(isNullable => isNullable);
            (await ReadMigrationAppliedAsync(
                    verificationContext,
                    CurrentMigration))
                .Should().BeFalse();
            string? storedNeighborhood = await verificationContext.Database
                .SqlQuery<string?>(
                    $"""
                     SELECT "Neighborhood" AS "Value"
                     FROM "ListingTranslations"
                     WHERE "Id" = {translationId}
                     """)
                .SingleAsync();
            storedNeighborhood.Should().Be(" ");
        }
    }

    private static async Task<IReadOnlyDictionary<string, string>>
        ReadOwnedConstraintsAsync(RealEstateDbContext dbContext)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        bool openedHere = connection.State != System.Data.ConnectionState.Open;

        if (openedHere)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT conname, pg_get_constraintdef(oid)
                FROM pg_catalog.pg_constraint
                WHERE conrelid = 'public."ListingTranslations"'::regclass
                  AND conname IN (
                      'CK_ListingTranslations_AddressLine_TrimmedNonBlank',
                      'CK_ListingTranslations_Municipality_TrimmedNonBlank',
                      'CK_ListingTranslations_Neighborhood_TrimmedNonBlank')
                ORDER BY conname;
                """;

            var constraints = new Dictionary<string, string>(
                StringComparer.Ordinal);
            await using DbDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                constraints.Add(reader.GetString(0), reader.GetString(1));
            }

            return constraints;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<IReadOnlyDictionary<string, bool>>
        ReadColumnNullabilityAsync(RealEstateDbContext dbContext)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        bool openedHere = connection.State != System.Data.ConnectionState.Open;

        if (openedHere)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT column_name, is_nullable = 'YES'
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = 'ListingTranslations'
                  AND column_name IN (
                      'AddressLine',
                      'Municipality',
                      'Neighborhood')
                ORDER BY column_name;
                """;

            var nullability = new Dictionary<string, bool>(
                StringComparer.Ordinal);
            await using DbDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                nullability.Add(reader.GetString(0), reader.GetBoolean(1));
            }

            return nullability;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<int> CountEarlierRowConstraintsAsync(
        RealEstateDbContext dbContext)
    {
        return await dbContext.Database.SqlQuery<int>(
                $"""
                 SELECT count(*)::integer AS "Value"
                 FROM pg_catalog.pg_constraint
                 WHERE conrelid = 'public."ListingTranslations"'::regclass
                   AND conname IN (
                       'CK_ListingTranslations_LanguageCode_Canonical',
                       'CK_ListingTranslations_Title_TrimmedNonBlank',
                       'CK_ListingTranslations_City_TrimmedNonBlank',
                       'CK_ListingTranslations_Description_TrimmedNonBlank')
                 """)
            .SingleAsync();
    }

    private static async Task<bool> ReadMigrationAppliedAsync(
        RealEstateDbContext dbContext,
        string migrationId)
    {
        return await dbContext.Database.SqlQuery<int>(
                $"""
                 SELECT count(*)::integer AS "Value"
                 FROM "__EFMigrationsHistory"
                 WHERE "MigrationId" = {migrationId}
                 """)
            .SingleAsync() == 1;
    }

    private static async Task InsertDraftListingAndTranslationAsync(
        RealEstateDbContext dbContext,
        Guid listingId,
        Guid translationId,
        string? neighborhood)
    {
        DateTime createdAtUtc = DateTime.UtcNow;
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "Listings"
                 ("Id", "ListingType", "PropertyType", "Status", "Price",
                  "Currency", "AreaSquareMeters", "CreatedAtUtc")
             VALUES
                 ({listingId}, 'Sale', 'Apartment', 'Draft', 100000,
                  'EUR', 80, {createdAtUtc})
             """);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "ListingTranslations"
                 ("Id", "ListingId", "LanguageCode", "Title", "Neighborhood")
             VALUES
                 ({translationId}, {listingId}, 'en', 'Migration test', {neighborhood})
             """);
    }

    private async Task<IsolatedMigrationDatabase>
        CreateIsolatedMigrationDatabaseAsync()
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        string mainConnectionString = dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "Integration PostgreSQL connection string is unavailable.");
        string databaseName = $"re_13h2_{Guid.NewGuid():N}";
        var targetBuilder = new NpgsqlConnectionStringBuilder(mainConnectionString)
        {
            Database = databaseName,
            Pooling = false
        };

        await using var adminConnection =
            new NpgsqlConnection(mainConnectionString);
        await adminConnection.OpenAsync();
        await using NpgsqlCommand command = adminConnection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{databaseName}\";";
        await command.ExecuteNonQueryAsync();

        return new IsolatedMigrationDatabase(
            mainConnectionString,
            targetBuilder.ConnectionString,
            databaseName);
    }

    private sealed class IsolatedMigrationDatabase(
        string adminConnectionString,
        string connectionString,
        string databaseName) : IAsyncDisposable
    {
        public RealEstateDbContext CreateContext()
        {
            DbContextOptions<RealEstateDbContext> options =
                new DbContextOptionsBuilder<RealEstateDbContext>()
                    .UseNpgsql(connectionString)
                    .Options;
            return new RealEstateDbContext(options);
        }

        public async ValueTask DisposeAsync()
        {
            if (!databaseName.StartsWith("re_13h2_", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Refusing to drop a database outside the isolated 13H.2 test namespace.");
            }

            await using (var pooledConnection =
                         new NpgsqlConnection(connectionString))
            {
                NpgsqlConnection.ClearPool(pooledConnection);
            }

            await using var adminConnection =
                new NpgsqlConnection(adminConnectionString);
            await adminConnection.OpenAsync();
            await using NpgsqlCommand command = adminConnection.CreateCommand();
            command.CommandText = $"DROP DATABASE \"{databaseName}\" WITH (FORCE);";
            await command.ExecuteNonQueryAsync();
        }
    }
}
