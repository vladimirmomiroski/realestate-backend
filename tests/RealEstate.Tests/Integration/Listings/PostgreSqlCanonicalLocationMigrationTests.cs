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

public sealed class PostgreSqlCanonicalLocationMigrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string PreviousMigration =
        "20260812172728_EnforceOptionalLocalizedLocationRowIntegrity";
    private const string CurrentMigration =
        "20260813100457_AddCanonicalGeocodedLocationSnapshot";

    private static readonly string[] NewColumns =
    [
        "GeocodedDisplayName",
        "GeocodingProviderKey",
        "GeocodingResultReference",
        "LocationConfirmedAtUtc",
        "LocationPrecision"
    ];

    private static readonly string[] ConstraintNames =
    [
        ListingConfiguration.CoordinatePairConstraintName,
        ListingConfiguration.LatitudeRangeConstraintName,
        ListingConfiguration.LongitudeRangeConstraintName,
        ListingConfiguration.LocationPrecisionConstraintName,
        ListingConfiguration.ProviderKeyConstraintName,
        ListingConfiguration.ResultReferenceConstraintName,
        ListingConfiguration.DisplayNameConstraintName,
        ListingConfiguration.SnapshotStateConstraintName
    ];

    private readonly CustomWebApplicationFactory _factory;

    public PostgreSqlCanonicalLocationMigrationTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Catalog_ContainsExpectedNullableColumnsAndNamedConstraints()
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        IReadOnlyDictionary<string, ColumnShape> columns =
            await ReadNewColumnShapesAsync(dbContext);
        IReadOnlyDictionary<string, string> constraints =
            await ReadOwnedConstraintsAsync(dbContext);

        columns.Keys.Should().BeEquivalentTo(NewColumns);
        columns["LocationPrecision"].Should().Be(
            new ColumnShape("character varying", 32, true));
        columns["GeocodingProviderKey"].Should().Be(
            new ColumnShape("character varying", 64, true));
        columns["GeocodingResultReference"].Should().Be(
            new ColumnShape("character varying", 512, true));
        columns["GeocodedDisplayName"].Should().Be(
            new ColumnShape("character varying", 500, true));
        columns["LocationConfirmedAtUtc"].Should().Be(
            new ColumnShape("timestamp with time zone", null, true));

        constraints.Keys.Should().BeEquivalentTo(ConstraintNames);
        constraints[ListingConfiguration.CoordinatePairConstraintName]
            .Should().ContainAll("Latitude", "Longitude", "IS NULL", "IS NOT NULL");
        constraints[ListingConfiguration.LatitudeRangeConstraintName]
            .Should().ContainAll("Latitude", "-90", "90");
        constraints[ListingConfiguration.LongitudeRangeConstraintName]
            .Should().ContainAll("Longitude", "-180", "180");
        constraints[ListingConfiguration.LocationPrecisionConstraintName]
            .Should().ContainAll(
                "ExactAddress",
                "Street",
                "Neighborhood",
                "Municipality",
                "City",
                "Approximate");
        constraints[ListingConfiguration.ProviderKeyConstraintName]
            .Should().ContainAll("GeocodingProviderKey", "btrim", "chr(160)", "chr(12288)");
        constraints[ListingConfiguration.ResultReferenceConstraintName]
            .Should().ContainAll("GeocodingResultReference", "btrim", "chr(8195)");
        constraints[ListingConfiguration.DisplayNameConstraintName]
            .Should().ContainAll("GeocodedDisplayName", "btrim", "chr(12288)");
        constraints[ListingConfiguration.SnapshotStateConstraintName]
            .Should().ContainAll(
                "Latitude",
                "Longitude",
                "LocationPrecision",
                "GeocodingProviderKey",
                "GeocodingResultReference",
                "GeocodedDisplayName",
                "LocationConfirmedAtUtc");

        CoordinateShape coordinateShape =
            await ReadCoordinateShapeAsync(dbContext);
        coordinateShape.Should().Be(new CoordinateShape(9, 6, 9, 6));
    }

    [Fact]
    public async Task FreshDatabase_MigratesDirectlyToH3()
    {
        await using IsolatedMigrationDatabase database =
            await CreateIsolatedMigrationDatabaseAsync();
        await using RealEstateDbContext dbContext = database.CreateContext();
        IMigrator migrator = dbContext.GetService<IMigrator>();

        await migrator.MigrateAsync(CurrentMigration);

        (await ReadOwnedConstraintsAsync(dbContext)).Should().HaveCount(8);
        (await ReadNewColumnShapesAsync(dbContext)).Should().HaveCount(5);
        (await CountH2TranslationConstraintsAsync(dbContext)).Should().Be(3);
        (await ReadMigrationAppliedAsync(dbContext, CurrentMigration))
            .Should().BeTrue();
    }

    [Fact]
    public async Task MigrationLifecycle_PreservesUnresolvedAndLegacyRowsAndRestoresH2OnDown()
    {
        await using IsolatedMigrationDatabase database =
            await CreateIsolatedMigrationDatabaseAsync();
        Guid unresolvedId = Guid.NewGuid();
        Guid legacyId = Guid.NewGuid();

        await using RealEstateDbContext dbContext = database.CreateContext();
        IMigrator migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        await InsertPreH3ListingAsync(dbContext, unresolvedId, null, null);
        await InsertPreH3ListingAsync(dbContext, legacyId, 41.9981m, 21.4254m);

        await migrator.MigrateAsync(CurrentMigration);
        (await ReadOwnedConstraintsAsync(dbContext)).Should().HaveCount(8);
        (await ReadNewColumnShapesAsync(dbContext)).Should().HaveCount(5);
        (await ReadMigrationAppliedAsync(dbContext, CurrentMigration))
            .Should().BeTrue();

        LocationRow unresolved = await ReadLocationRowAsync(dbContext, unresolvedId);
        unresolved.Should().Be(LocationRow.Unresolved);
        LocationRow legacy = await ReadLocationRowAsync(dbContext, legacyId);
        legacy.Should().Be(new LocationRow(
            41.9981m,
            21.4254m,
            null,
            null,
            null,
            null,
            null));

        await migrator.MigrateAsync(CurrentMigration);
        (await ReadOwnedConstraintsAsync(dbContext)).Should().HaveCount(8);

        await migrator.MigrateAsync(PreviousMigration);
        (await ReadOwnedConstraintsAsync(dbContext)).Should().BeEmpty();
        (await ReadNewColumnShapesAsync(dbContext)).Should().BeEmpty();
        (await CountH2TranslationConstraintsAsync(dbContext)).Should().Be(3);
        (await ReadMigrationAppliedAsync(dbContext, CurrentMigration))
            .Should().BeFalse();
        (await ReadPreH3CoordinatesAsync(dbContext, unresolvedId))
            .Should().Be((null, null));
        (await ReadPreH3CoordinatesAsync(dbContext, legacyId))
            .Should().Be((41.9981m, 21.4254m));
        (await ReadCoordinateShapeAsync(dbContext)).Should().Be(
            new CoordinateShape(9, 6, 9, 6));

        await migrator.MigrateAsync(CurrentMigration);
        (await ReadOwnedConstraintsAsync(dbContext)).Should().HaveCount(8);
        (await ReadNewColumnShapesAsync(dbContext)).Should().HaveCount(5);
        (await ReadLocationRowAsync(dbContext, legacyId)).Should().Be(legacy);
    }

    [Fact]
    public async Task MigrationWithOutOfRangeLegacyCoordinates_FailsWithoutPartialInstallationOrRepair()
    {
        await using IsolatedMigrationDatabase database =
            await CreateIsolatedMigrationDatabaseAsync();
        Guid listingId = Guid.NewGuid();

        await using (RealEstateDbContext setupContext = database.CreateContext())
        {
            IMigrator migrator = setupContext.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            await InsertPreH3ListingAsync(setupContext, listingId, 91m, 21m);
        }

        await using (RealEstateDbContext migrationContext = database.CreateContext())
        {
            IMigrator migrator = migrationContext.GetService<IMigrator>();
            Func<Task> action = () => migrator.MigrateAsync(CurrentMigration);

            PostgresException exception =
                (await action.Should().ThrowAsync<PostgresException>()).Which;
            exception.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
            exception.ConstraintName.Should().Be(
                ListingConfiguration.LatitudeRangeConstraintName);
        }

        await using (RealEstateDbContext verificationContext = database.CreateContext())
        {
            (await ReadOwnedConstraintsAsync(verificationContext))
                .Should().BeEmpty();
            (await ReadNewColumnShapesAsync(verificationContext))
                .Should().BeEmpty();
            (await ReadMigrationAppliedAsync(
                    verificationContext,
                    CurrentMigration))
                .Should().BeFalse();
            (await CountH2TranslationConstraintsAsync(verificationContext))
                .Should().Be(3);
            (await ReadPreH3CoordinatesAsync(verificationContext, listingId))
                .Should().Be((91m, 21m));
        }
    }

    private static async Task InsertPreH3ListingAsync(
        RealEstateDbContext dbContext,
        Guid listingId,
        decimal? latitude,
        decimal? longitude)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "Listings"
                 ("Id", "ListingType", "PropertyType", "Status", "Price",
                  "Currency", "AreaSquareMeters", "Latitude", "Longitude",
                  "CreatedAtUtc")
             VALUES
                 ({listingId}, 'Sale', 'Apartment', 'Draft', 100000,
                  'EUR', 80, {latitude}, {longitude}, {DateTime.UtcNow})
             """);
    }

    private static async Task<IReadOnlyDictionary<string, ColumnShape>>
        ReadNewColumnShapesAsync(RealEstateDbContext dbContext)
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
                SELECT column_name, data_type, character_maximum_length,
                       is_nullable = 'YES'
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = 'Listings'
                  AND column_name IN (
                      'GeocodedDisplayName',
                      'GeocodingProviderKey',
                      'GeocodingResultReference',
                      'LocationConfirmedAtUtc',
                      'LocationPrecision')
                ORDER BY column_name;
                """;

            var columns = new Dictionary<string, ColumnShape>(StringComparer.Ordinal);
            await using DbDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(
                    reader.GetString(0),
                    new ColumnShape(
                        reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetInt32(2),
                        reader.GetBoolean(3)));
            }

            return columns;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
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
                WHERE conrelid = 'public."Listings"'::regclass
                  AND conname LIKE 'CK_Listings_Location_%'
                ORDER BY conname;
                """;

            var constraints = new Dictionary<string, string>(StringComparer.Ordinal);
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

    private static async Task<CoordinateShape> ReadCoordinateShapeAsync(
        RealEstateDbContext dbContext)
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
                SELECT
                    max(numeric_precision) FILTER (
                        WHERE column_name = 'Latitude')::integer,
                    max(numeric_scale) FILTER (
                        WHERE column_name = 'Latitude')::integer,
                    max(numeric_precision) FILTER (
                        WHERE column_name = 'Longitude')::integer,
                    max(numeric_scale) FILTER (
                        WHERE column_name = 'Longitude')::integer
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = 'Listings'
                  AND column_name IN ('Latitude', 'Longitude');
                """;
            await using DbDataReader reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();
            return new CoordinateShape(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt32(3));
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<int> CountH2TranslationConstraintsAsync(
        RealEstateDbContext dbContext)
    {
        return await dbContext.Database.SqlQuery<int>(
                $"""
                 SELECT count(*)::integer AS "Value"
                 FROM pg_catalog.pg_constraint
                 WHERE conrelid = 'public."ListingTranslations"'::regclass
                   AND conname IN (
                       'CK_ListingTranslations_AddressLine_TrimmedNonBlank',
                       'CK_ListingTranslations_Municipality_TrimmedNonBlank',
                       'CK_ListingTranslations_Neighborhood_TrimmedNonBlank')
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

    private static async Task<LocationRow> ReadLocationRowAsync(
        RealEstateDbContext dbContext,
        Guid listingId)
    {
        return await dbContext.Database.SqlQuery<LocationRow>(
                $"""
                 SELECT "Latitude", "Longitude", "LocationPrecision",
                        "GeocodingProviderKey", "GeocodingResultReference",
                        "GeocodedDisplayName", "LocationConfirmedAtUtc"
                 FROM "Listings"
                 WHERE "Id" = {listingId}
                 """)
            .SingleAsync();
    }

    private static async Task<(decimal? Latitude, decimal? Longitude)>
        ReadPreH3CoordinatesAsync(
            RealEstateDbContext dbContext,
            Guid listingId)
    {
        CoordinateRow row = await dbContext.Database.SqlQuery<CoordinateRow>(
                $"""
                 SELECT "Latitude", "Longitude"
                 FROM "Listings"
                 WHERE "Id" = {listingId}
                 """)
            .SingleAsync();
        return (row.Latitude, row.Longitude);
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
        string databaseName = $"re_13h3_{Guid.NewGuid():N}";
        var targetBuilder = new NpgsqlConnectionStringBuilder(mainConnectionString)
        {
            Database = databaseName,
            Pooling = false
        };

        await using var adminConnection = new NpgsqlConnection(mainConnectionString);
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
            if (!databaseName.StartsWith("re_13h3_", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Refusing to drop a database outside the isolated 13H.3 test namespace.");
            }

            await using (var pooledConnection = new NpgsqlConnection(connectionString))
            {
                NpgsqlConnection.ClearPool(pooledConnection);
            }

            await using var adminConnection = new NpgsqlConnection(adminConnectionString);
            await adminConnection.OpenAsync();
            await using NpgsqlCommand command = adminConnection.CreateCommand();
            command.CommandText = $"DROP DATABASE \"{databaseName}\" WITH (FORCE);";
            await command.ExecuteNonQueryAsync();
        }
    }

    private sealed record ColumnShape(
        string DataType,
        int? MaximumLength,
        bool IsNullable);

    private sealed record CoordinateShape(
        int LatitudePrecision,
        int LatitudeScale,
        int LongitudePrecision,
        int LongitudeScale);

    private sealed record CoordinateRow(
        decimal? Latitude,
        decimal? Longitude);

    private sealed record LocationRow(
        decimal? Latitude,
        decimal? Longitude,
        string? LocationPrecision,
        string? GeocodingProviderKey,
        string? GeocodingResultReference,
        string? GeocodedDisplayName,
        DateTime? LocationConfirmedAtUtc)
    {
        public static LocationRow Unresolved { get; } = new(
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }
}
