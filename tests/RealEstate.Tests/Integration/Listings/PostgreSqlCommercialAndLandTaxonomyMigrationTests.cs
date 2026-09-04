using System.Data;
using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;

namespace RealEstate.Tests.Integration.Listings;

public sealed class PostgreSqlCommercialAndLandTaxonomyMigrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string PreviousMigration =
        "20260824141614_EnforceStrongActiveLocationIntegrity";
    private const string CurrentMigration =
        "20260904023937_AddCommercialAndLandPropertyTaxonomy";

    private static readonly string[] NewTableNames =
    [
        "ListingCommercialDetails",
        "ListingLandDetails"
    ];

    private readonly CustomWebApplicationFactory _factory;

    public PostgreSqlCommercialAndLandTaxonomyMigrationTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Catalog_contains_exact_structural_subtype_schema()
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        IReadOnlyList<ColumnCatalogRow> columns =
            await ReadColumnsAsync(dbContext);

        columns.Should().BeEquivalentTo(
        [
            new ColumnCatalogRow(
                "ListingCommercialDetails",
                "ListingId",
                "uuid",
                null,
                false,
                null),
            new ColumnCatalogRow(
                "ListingCommercialDetails",
                "CommercialType",
                "character varying",
                50,
                false,
                "'Unknown'::character varying"),
            new ColumnCatalogRow(
                "ListingLandDetails",
                "ListingId",
                "uuid",
                null,
                false,
                null),
            new ColumnCatalogRow(
                "ListingLandDetails",
                "LandType",
                "character varying",
                50,
                false,
                "'Unknown'::character varying")
        ], options => options.WithStrictOrdering());

        IReadOnlyList<ConstraintCatalogRow> constraints =
            await ReadConstraintsAsync(dbContext);
        constraints.Should().BeEquivalentTo(
        [
            new ConstraintCatalogRow(
                "ListingCommercialDetails",
                "PK_ListingCommercialDetails",
                "p",
                "PRIMARY KEY (\"ListingId\")"),
            new ConstraintCatalogRow(
                "ListingCommercialDetails",
                "FK_ListingCommercialDetails_Listings_ListingId",
                "f",
                "FOREIGN KEY (\"ListingId\") REFERENCES \"Listings\"(\"Id\") ON DELETE CASCADE"),
            new ConstraintCatalogRow(
                "ListingLandDetails",
                "PK_ListingLandDetails",
                "p",
                "PRIMARY KEY (\"ListingId\")"),
            new ConstraintCatalogRow(
                "ListingLandDetails",
                "FK_ListingLandDetails_Listings_ListingId",
                "f",
                "FOREIGN KEY (\"ListingId\") REFERENCES \"Listings\"(\"Id\") ON DELETE CASCADE")
        ]);

        IReadOnlyList<IndexCatalogRow> indexes =
            await ReadIndexesAsync(dbContext);
        indexes.Should().HaveCount(2);
        indexes.Should().ContainEquivalentOf(new IndexCatalogRow(
            "ListingCommercialDetails",
            "PK_ListingCommercialDetails",
            "CREATE UNIQUE INDEX \"PK_ListingCommercialDetails\" ON public.\"ListingCommercialDetails\" USING btree (\"ListingId\")"));
        indexes.Should().ContainEquivalentOf(new IndexCatalogRow(
            "ListingLandDetails",
            "PK_ListingLandDetails",
            "CREATE UNIQUE INDEX \"PK_ListingLandDetails\" ON public.\"ListingLandDetails\" USING btree (\"ListingId\")"));
    }

    [Fact]
    public async Task Fresh_database_applies_all_twenty_one_migrations_and_repeat_is_no_op()
    {
        await using IsolatedMigrationDatabase database =
            await CreateIsolatedMigrationDatabaseAsync();
        await using RealEstateDbContext dbContext = database.CreateContext();
        IMigrator migrator = dbContext.GetService<IMigrator>();

        await migrator.MigrateAsync(CurrentMigration);
        (await CountAppliedMigrationsAsync(dbContext)).Should().Be(21);
        (await ReadPublicTableNamesAsync(dbContext))
            .Should().Contain(NewTableNames);

        await migrator.MigrateAsync(CurrentMigration);
        (await CountAppliedMigrationsAsync(dbContext)).Should().Be(21);
    }

    [Fact]
    public async Task Migration_twenty_to_twenty_one_preserves_existing_data_down_and_re_up()
    {
        await using IsolatedMigrationDatabase database =
            await CreateIsolatedMigrationDatabaseAsync();
        Guid apartmentId = Guid.NewGuid();
        Guid houseId = Guid.NewGuid();
        IReadOnlyList<string> migrationTwentyTables;

        await using (RealEstateDbContext setupContext = database.CreateContext())
        {
            IMigrator migrator = setupContext.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            (await CountAppliedMigrationsAsync(setupContext)).Should().Be(20);
            migrationTwentyTables =
                await ReadPublicTableNamesAsync(setupContext);

            Listing apartment = CreateExistingListing(
                apartmentId,
                PropertyType.Apartment,
                "Existing apartment",
                41.9981m,
                21.4254m);
            apartment.ApartmentDetails = new ListingApartmentDetails
            {
                ListingId = apartmentId,
                ApartmentType = ApartmentType.Standard,
                Floor = 3,
                TotalFloors = 8,
                HasElevator = true
            };

            Listing house = CreateExistingListing(
                houseId,
                PropertyType.House,
                "Existing house",
                42.0040m,
                21.4095m);
            house.HouseDetails = new ListingHouseDetails
            {
                ListingId = houseId,
                HouseType = HouseType.Detached,
                NumberOfFloors = 2,
                YardAreaSquareMeters = 180m
            };

            setupContext.Listings.AddRange(apartment, house);
            await setupContext.SaveChangesAsync();
        }

        await MigrateAndAssertAsync(
            database,
            CurrentMigration,
            expectedMigrationCount: 21,
            expectNewTables: true,
            apartmentId,
            houseId);

        await MigrateAndAssertAsync(
            database,
            CurrentMigration,
            expectedMigrationCount: 21,
            expectNewTables: true,
            apartmentId,
            houseId);

        await MigrateAndAssertAsync(
            database,
            PreviousMigration,
            expectedMigrationCount: 20,
            expectNewTables: false,
            apartmentId,
            houseId);

        await using (RealEstateDbContext downContext = database.CreateContext())
        {
            (await ReadPublicTableNamesAsync(downContext))
                .Should().Equal(migrationTwentyTables);
        }

        await MigrateAndAssertAsync(
            database,
            CurrentMigration,
            expectedMigrationCount: 21,
            expectNewTables: true,
            apartmentId,
            houseId);
    }

    private static Listing CreateExistingListing(
        Guid listingId,
        PropertyType propertyType,
        string title,
        decimal latitude,
        decimal longitude)
    {
        var listing = new Listing
        {
            Id = listingId,
            ListingType = ListingType.Sale,
            PropertyType = propertyType,
            Price = 125_000m,
            Currency = "EUR",
            AreaSquareMeters = 75m,
            Translations =
            [
                new ListingTranslation
                {
                    Id = Guid.NewGuid(),
                    ListingId = listingId,
                    LanguageCode = "en",
                    Title = title,
                    Description = "Existing pre-Chapter-14 listing.",
                    AddressLine = "Migration Street 14",
                    City = "Skopje",
                    Municipality = "Centar",
                    Neighborhood = "Center"
                }
            ]
        };

        listing.ConfirmLocation(
            latitude,
            longitude,
            LocationPrecision.ExactAddress,
            "migration-test",
            $"existing-{listingId:N}",
            $"{title} location",
            new DateTime(2026, 8, 24, 16, 30, 0, DateTimeKind.Utc));

        return listing;
    }

    private static async Task MigrateAndAssertAsync(
        IsolatedMigrationDatabase database,
        string targetMigration,
        int expectedMigrationCount,
        bool expectNewTables,
        Guid apartmentId,
        Guid houseId)
    {
        await using RealEstateDbContext dbContext = database.CreateContext();
        await dbContext.GetService<IMigrator>().MigrateAsync(targetMigration);

        (await CountAppliedMigrationsAsync(dbContext))
            .Should().Be(expectedMigrationCount);
        IReadOnlyList<string> tableNames =
            await ReadPublicTableNamesAsync(dbContext);
        foreach (string tableName in NewTableNames)
        {
            tableNames.Contains(tableName).Should().Be(expectNewTables);
        }

        Listing apartment = await dbContext.Listings
            .AsNoTracking()
            .Include(listing => listing.ApartmentDetails)
            .Include(listing => listing.Translations)
            .SingleAsync(listing => listing.Id == apartmentId);
        apartment.PropertyType.Should().Be(PropertyType.Apartment);
        apartment.ApartmentDetails!.ApartmentType
            .Should().Be(ApartmentType.Standard);
        apartment.Translations.Single().Title
            .Should().Be("Existing apartment");
        apartment.Latitude.Should().Be(41.9981m);
        apartment.Longitude.Should().Be(21.4254m);
        apartment.LocationPrecision.Should().Be(LocationPrecision.ExactAddress);
        apartment.GeocodingProviderKey.Should().Be("migration-test");
        apartment.LocationConfirmedAtUtc.Should().NotBeNull();

        Listing house = await dbContext.Listings
            .AsNoTracking()
            .Include(listing => listing.HouseDetails)
            .Include(listing => listing.Translations)
            .SingleAsync(listing => listing.Id == houseId);
        house.PropertyType.Should().Be(PropertyType.House);
        house.HouseDetails!.HouseType.Should().Be(HouseType.Detached);
        house.Translations.Single().Title.Should().Be("Existing house");
        house.Latitude.Should().Be(42.0040m);
        house.Longitude.Should().Be(21.4095m);
        house.LocationPrecision.Should().Be(LocationPrecision.ExactAddress);
        house.GeocodingProviderKey.Should().Be("migration-test");
        house.LocationConfirmedAtUtc.Should().NotBeNull();
    }

    private static async Task<IReadOnlyList<ColumnCatalogRow>>
        ReadColumnsAsync(RealEstateDbContext dbContext)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT table_name,
                   column_name,
                   data_type,
                   character_maximum_length,
                   is_nullable = 'YES',
                   column_default
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = ANY(@table_names)
            ORDER BY table_name, ordinal_position;
            """;
        command.Parameters.Add(new NpgsqlParameter<string[]>(
            "table_names",
            NewTableNames));

        var rows = new List<ColumnCatalogRow>();
        await using DbDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new ColumnCatalogRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.GetBoolean(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ConstraintCatalogRow>>
        ReadConstraintsAsync(RealEstateDbContext dbContext)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT table_class.relname,
                   constraint_object.conname,
                   constraint_object.contype::text,
                   pg_get_constraintdef(constraint_object.oid)
            FROM pg_catalog.pg_constraint AS constraint_object
            JOIN pg_catalog.pg_class AS table_class
              ON table_class.oid = constraint_object.conrelid
            JOIN pg_catalog.pg_namespace AS table_namespace
              ON table_namespace.oid = table_class.relnamespace
            WHERE table_namespace.nspname = 'public'
              AND table_class.relname = ANY(@table_names)
            ORDER BY table_class.relname, constraint_object.contype DESC;
            """;
        command.Parameters.Add(new NpgsqlParameter<string[]>(
            "table_names",
            NewTableNames));

        var rows = new List<ConstraintCatalogRow>();
        await using DbDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new ConstraintCatalogRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<IndexCatalogRow>>
        ReadIndexesAsync(RealEstateDbContext dbContext)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT tablename, indexname, indexdef
            FROM pg_catalog.pg_indexes
            WHERE schemaname = 'public'
              AND tablename = ANY(@table_names)
            ORDER BY tablename, indexname;
            """;
        command.Parameters.Add(new NpgsqlParameter<string[]>(
            "table_names",
            NewTableNames));

        var rows = new List<IndexCatalogRow>();
        await using DbDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new IndexCatalogRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<string>>
        ReadPublicTableNamesAsync(RealEstateDbContext dbContext)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_type = 'BASE TABLE'
            ORDER BY table_name;
            """;

        var tableNames = new List<string>();
        await using DbDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tableNames.Add(reader.GetString(0));
        }

        return tableNames;
    }

    private static async Task<int> CountAppliedMigrationsAsync(
        RealEstateDbContext dbContext)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT count(*)::integer FROM public.\"__EFMigrationsHistory\";";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
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
        string databaseName = $"re_14b_{Guid.NewGuid():N}";
        var targetBuilder = new NpgsqlConnectionStringBuilder(
            mainConnectionString)
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
            if (!databaseName.StartsWith("re_14b_", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Refusing to drop a database outside the isolated 14B test namespace.");
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
            command.CommandText =
                $"DROP DATABASE \"{databaseName}\" WITH (FORCE);";
            await command.ExecuteNonQueryAsync();
        }
    }

    private sealed record ColumnCatalogRow(
        string TableName,
        string ColumnName,
        string DataType,
        int? MaximumLength,
        bool IsNullable,
        string? DefaultValue);

    private sealed record ConstraintCatalogRow(
        string TableName,
        string ConstraintName,
        string ConstraintType,
        string Definition);

    private sealed record IndexCatalogRow(
        string TableName,
        string IndexName,
        string Definition);
}
