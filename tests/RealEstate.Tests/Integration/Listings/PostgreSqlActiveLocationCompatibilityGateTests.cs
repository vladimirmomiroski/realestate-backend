using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;
using RealEstate.Infrastructure.Persistence;

namespace RealEstate.Tests.Integration.Listings;

public sealed class PostgreSqlActiveLocationCompatibilityGateTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string MunicipalityFamily =
        "translation_missing_or_blank_municipality";
    private const string AddressLineFamily =
        "translation_missing_or_blank_address_line";
    private const string UnresolvedFamily = "root_unresolved";
    private const string LegacyFamily = "root_legacy_unverified";
    private const string PartialFamily = "root_partial";
    private const string InvalidConfirmedFamily = "root_invalid_confirmed";

    private static readonly DateTime ConfirmationTime =
        new(2026, 8, 24, 10, 0, 0, DateTimeKind.Utc);

    private static readonly Lazy<string> CompatibilityReportSql =
        new(ReadCompatibilityReportSql);

    private readonly CustomWebApplicationFactory _factory;

    public PostgreSqlActiveLocationCompatibilityGateTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CompatibilityReport_ClassifiesExactTargetDeltaMatrix()
    {
        await using NpgsqlConnection connection = await OpenProbeAsync();
        var expected = new Dictionary<Guid, string[]>();

        await SeedCaseAsync(
            connection,
            1,
            ValidConfirmed,
            municipality: null,
            addressLine: "Address",
            expected,
            MunicipalityFamily);
        await SeedCaseAsync(
            connection,
            2,
            ValidConfirmed,
            municipality: "\u2003",
            addressLine: "Address",
            expected,
            MunicipalityFamily);
        await SeedCaseAsync(
            connection,
            3,
            ValidConfirmed,
            municipality: "Centar",
            addressLine: null,
            expected,
            AddressLineFamily);
        await SeedCaseAsync(
            connection,
            4,
            ValidConfirmed,
            municipality: "Centar",
            addressLine: "\u00A0",
            expected,
            AddressLineFamily);
        await SeedCaseAsync(
            connection,
            5,
            Snapshot.Unresolved,
            "Centar",
            "Address",
            expected,
            UnresolvedFamily);
        await SeedCaseAsync(
            connection,
            6,
            Snapshot.Legacy,
            "Centar",
            "Address",
            expected,
            LegacyFamily);
        await SeedCaseAsync(
            connection,
            7,
            Snapshot.PartialCoordinate,
            "Centar",
            "Address",
            expected,
            PartialFamily);
        await SeedCaseAsync(
            connection,
            8,
            Snapshot.PartialMetadata,
            "Centar",
            "Address",
            expected,
            PartialFamily);
        await SeedCaseAsync(
            connection,
            9,
            ValidConfirmed with { Latitude = 90.000001m },
            "Centar",
            "Address",
            expected,
            InvalidConfirmedFamily);
        await SeedCaseAsync(
            connection,
            10,
            ValidConfirmed with { Precision = "BuildingRoof" },
            "Centar",
            "Address",
            expected,
            InvalidConfirmedFamily);
        await SeedCaseAsync(
            connection,
            11,
            ValidConfirmed with { ProviderKey = " provider" },
            "Centar",
            "Address",
            expected,
            InvalidConfirmedFamily);

        await SeedCaseAsync(
            connection,
            12,
            ValidConfirmed,
            "Centar",
            "Address");
        await SeedCaseAsync(
            connection,
            13,
            ValidConfirmed with { DisplayName = null },
            "Centar",
            "Address");
        await SeedCaseAsync(
            connection,
            14,
            Snapshot.Unresolved,
            municipality: null,
            addressLine: null,
            expected,
            MunicipalityFamily,
            AddressLineFamily,
            UnresolvedFamily);
        await SeedCaseAsync(
            connection,
            15,
            Snapshot.Unresolved,
            "Centar",
            "Address",
            status: "Draft");

        Guid anyTranslationId = CaseId(16);
        await InsertListingAsync(connection, anyTranslationId, ValidConfirmed);
        await InsertTranslationAsync(
            connection,
            anyTranslationId,
            "Centar",
            "Address");
        await InsertTranslationAsync(
            connection,
            anyTranslationId,
            municipality: null,
            "Second address");
        expected.Add(anyTranslationId, [MunicipalityFamily]);

        long activeCountBefore = await CountActiveListingsAsync(connection);
        CompatibilityReport report =
            await ExecuteInReadOnlyTransactionAsync(connection);
        long activeCountAfter = await CountActiveListingsAsync(connection);

        activeCountAfter.Should().Be(activeCountBefore);
        report.IncompatibleCount.Should().Be(expected.Count);
        report.Families.Keys.Should().BeEquivalentTo(expected.Keys);
        foreach ((Guid listingId, string[] families) in expected)
        {
            report.Families[listingId].Should().Equal(families);
        }
    }

    [Fact]
    public async Task CompatibilityReport_QueryPlanIsBoundedAndReadOnly()
    {
        string sql = CompatibilityReportSql.Value;
        Regex.IsMatch(
                sql,
                @"\b(INSERT|UPDATE|DELETE|MERGE|CALL|CREATE|ALTER|DROP|TRUNCATE|COPY)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Should().BeFalse();
        sql.Count(character => character == ';').Should().Be(1);
        sql.TrimEnd().Should().EndWith(";");

        await using NpgsqlConnection connection = await OpenProbeAsync();
        await SeedCaseAsync(
            connection,
            1,
            ValidConfirmed,
            "Centar",
            "Address");

        await using var command = new NpgsqlCommand(
            "EXPLAIN (FORMAT JSON, COSTS FALSE) " + sql,
            connection);
        string plan = (string)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException(
                "PostgreSQL did not return a compatibility-report plan."));

        plan.Should().NotContain("ModifyTable");
        Regex.Matches(plan, "\\\"Relation Name\\\": \\\"Listings\\\"")
            .Should().HaveCount(1);
        Regex.Matches(
                plan,
                "\\\"Relation Name\\\": \\\"ListingTranslations\\\"")
            .Should().HaveCount(1);
    }

    private async Task<NpgsqlConnection> OpenProbeAsync()
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        string connectionString = dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "Integration PostgreSQL connection string is unavailable.");

        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            CREATE TEMP TABLE "Listings"
            (
                "Id" uuid NOT NULL,
                "Status" text NOT NULL,
                "Latitude" numeric NULL,
                "Longitude" numeric NULL,
                "LocationPrecision" text NULL,
                "GeocodingProviderKey" text NULL,
                "GeocodingResultReference" text NULL,
                "GeocodedDisplayName" text NULL,
                "LocationConfirmedAtUtc" timestamp with time zone NULL
            );

            CREATE TEMP TABLE "ListingTranslations"
            (
                "ListingId" uuid NOT NULL,
                "Municipality" text NULL,
                "AddressLine" text NULL
            );
            """,
            connection);
        await command.ExecuteNonQueryAsync();

        return connection;
    }

    private static async Task SeedCaseAsync(
        NpgsqlConnection connection,
        int sequence,
        Snapshot snapshot,
        string? municipality,
        string? addressLine,
        Dictionary<Guid, string[]>? expected = null,
        params string[] expectedFamilies)
    {
        await SeedCaseAsync(
            connection,
            sequence,
            snapshot,
            municipality,
            addressLine,
            status: "Active");

        if (expected is not null)
        {
            expected.Add(CaseId(sequence), expectedFamilies);
        }
    }

    private static async Task SeedCaseAsync(
        NpgsqlConnection connection,
        int sequence,
        Snapshot snapshot,
        string? municipality,
        string? addressLine,
        string status)
    {
        Guid listingId = CaseId(sequence);
        await InsertListingAsync(connection, listingId, snapshot, status);
        await InsertTranslationAsync(
            connection,
            listingId,
            municipality,
            addressLine);
    }

    private static async Task InsertListingAsync(
        NpgsqlConnection connection,
        Guid listingId,
        Snapshot snapshot,
        string status = "Active")
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "Listings"
            (
                "Id",
                "Status",
                "Latitude",
                "Longitude",
                "LocationPrecision",
                "GeocodingProviderKey",
                "GeocodingResultReference",
                "GeocodedDisplayName",
                "LocationConfirmedAtUtc"
            )
            VALUES
            (
                @id,
                @status,
                @latitude,
                @longitude,
                @precision,
                @providerKey,
                @resultReference,
                @displayName,
                @confirmedAtUtc
            );
            """,
            connection);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, listingId);
        command.Parameters.AddWithValue("status", NpgsqlDbType.Text, status);
        AddNullable(
            command,
            "latitude",
            NpgsqlDbType.Numeric,
            snapshot.Latitude);
        AddNullable(
            command,
            "longitude",
            NpgsqlDbType.Numeric,
            snapshot.Longitude);
        AddNullable(command, "precision", NpgsqlDbType.Text, snapshot.Precision);
        AddNullable(
            command,
            "providerKey",
            NpgsqlDbType.Text,
            snapshot.ProviderKey);
        AddNullable(
            command,
            "resultReference",
            NpgsqlDbType.Text,
            snapshot.ResultReference);
        AddNullable(
            command,
            "displayName",
            NpgsqlDbType.Text,
            snapshot.DisplayName);
        AddNullable(
            command,
            "confirmedAtUtc",
            NpgsqlDbType.TimestampTz,
            snapshot.ConfirmedAtUtc);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertTranslationAsync(
        NpgsqlConnection connection,
        Guid listingId,
        string? municipality,
        string? addressLine)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "ListingTranslations"
                ("ListingId", "Municipality", "AddressLine")
            VALUES
                (@listingId, @municipality, @addressLine);
            """,
            connection);
        command.Parameters.AddWithValue(
            "listingId",
            NpgsqlDbType.Uuid,
            listingId);
        AddNullable(
            command,
            "municipality",
            NpgsqlDbType.Text,
            municipality);
        AddNullable(
            command,
            "addressLine",
            NpgsqlDbType.Text,
            addressLine);
        await command.ExecuteNonQueryAsync();
    }

    private static void AddNullable(
        NpgsqlCommand command,
        string name,
        NpgsqlDbType type,
        object? value)
    {
        command.Parameters.Add(new NpgsqlParameter(name, type)
        {
            Value = value ?? DBNull.Value
        });
    }

    private static async Task<CompatibilityReport>
        ExecuteInReadOnlyTransactionAsync(NpgsqlConnection connection)
    {
        await using (var begin = new NpgsqlCommand(
                         "BEGIN TRANSACTION READ ONLY;",
                         connection))
        {
            await begin.ExecuteNonQueryAsync();
        }

        try
        {
            await using var command = new NpgsqlCommand(
                CompatibilityReportSql.Value,
                connection);
            await using NpgsqlDataReader reader =
                await command.ExecuteReaderAsync(CommandBehavior.SingleRow);
            (await reader.ReadAsync()).Should().BeTrue();

            long count = reader.GetInt64(0);
            using JsonDocument document = JsonDocument.Parse(
                reader.GetString(1));
            var families = new Dictionary<Guid, string[]>();
            foreach (JsonElement item in document.RootElement.EnumerateArray())
            {
                item.EnumerateObject().Select(property => property.Name)
                    .Should().BeEquivalentTo("listingId", "families");
                Guid listingId = Guid.Parse(
                    item.GetProperty("listingId").GetString()!);
                string[] itemFamilies = item.GetProperty("families")
                    .EnumerateArray()
                    .Select(value => value.GetString()!)
                    .ToArray();
                families.Add(listingId, itemFamilies);
            }

            return new CompatibilityReport(count, families);
        }
        finally
        {
            await using var rollback = new NpgsqlCommand(
                "ROLLBACK;",
                connection);
            await rollback.ExecuteNonQueryAsync();
        }
    }

    private static async Task<long> CountActiveListingsAsync(
        NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM \"Listings\" WHERE \"Status\" = 'Active';",
            connection);
        return (long)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException(
                "PostgreSQL did not return the Active probe count."));
    }

    private static Guid CaseId(int sequence)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{sequence:D12}");
    }

    private static string ReadCompatibilityReportSql()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "RealEstate.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                "Could not locate the repository root for the active-location compatibility report.");
        }

        string path = Path.Combine(
            directory.FullName,
            "docs",
            "operations",
            "active-location-compatibility.sql");
        return File.ReadAllText(path, Encoding.UTF8);
    }

    private static Snapshot ValidConfirmed { get; } = new(
        41.9981m,
        21.4254m,
        "ExactAddress",
        "test-provider",
        "opaque-result-reference",
        "Confirmed display name",
        ConfirmationTime);

    private sealed record Snapshot(
        decimal? Latitude,
        decimal? Longitude,
        string? Precision,
        string? ProviderKey,
        string? ResultReference,
        string? DisplayName,
        DateTime? ConfirmedAtUtc)
    {
        public static Snapshot Unresolved { get; } = new(
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        public static Snapshot Legacy { get; } = Unresolved with
        {
            Latitude = 41.9981m,
            Longitude = 21.4254m
        };

        public static Snapshot PartialCoordinate { get; } = Unresolved with
        {
            Latitude = 41.9981m
        };

        public static Snapshot PartialMetadata { get; } = Unresolved with
        {
            Precision = "City"
        };
    }

    private sealed record CompatibilityReport(
        long IncompatibleCount,
        IReadOnlyDictionary<Guid, string[]> Families);
}
