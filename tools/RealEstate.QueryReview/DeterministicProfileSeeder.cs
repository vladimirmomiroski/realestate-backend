using Npgsql;

namespace RealEstate.QueryReview;

internal sealed record ListingPhysicalProfile(long HeapBytes, long HeapPages);

internal sealed record ListingPhysicalNormalizationResult(
    ListingPhysicalProfile Before,
    ListingPhysicalProfile After);

internal static class DeterministicProfileSeeder
{
    public const int CSharpSeed = 1_042_001;
    public const double PostgreSqlSeed = 0.1042001;
    public const int ListingCount = 100_000;
    public const string ProfileVersion = "chapter-10f-v1";
    public const string ComparableSourceId = "40000000-0000-0000-0000-000000000bb9";

    public static async Task<ProfileVerificationResult> CreateAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await EnsurePublicationIntegritySchemaAsync(
            connection,
            transaction,
            cancellationToken);
        await EnsureProfileTablesAreEmptyAsync(connection, transaction, cancellationToken);
        await ExecuteAsync(connection, transaction, "SELECT setseed(0.1042001);", cancellationToken);
        await ExecuteAsync(connection, transaction, SeedUsersAndAgenciesSql, cancellationToken);
        await ExecuteAsync(connection, transaction, SeedListingsSql, cancellationToken);
        await ExecuteAsync(connection, transaction, SeedTranslationsSql, cancellationToken);
        await ExecuteAsync(connection, transaction, SeedDetailsSql, cancellationToken);
        await ExecuteAsync(connection, transaction, SeedImagesSql, cancellationToken);
        await EnsureIntendedActiveAggregatesReadyAsync(
            connection,
            transaction,
            cancellationToken);
        await ExecuteAsync(connection, transaction, ApplyIntendedStatusesSql, cancellationToken);
        await EnsureFinalActiveAggregatesValidAsync(
            connection,
            transaction,
            cancellationToken);

        var verification = await ProfileInvariants.VerifyAsync(
            connection,
            transaction,
            cancellationToken);

        verification.EnsureValid();
        await transaction.CommitAsync(cancellationToken);

        return verification;
    }

    public static async Task<ListingPhysicalNormalizationResult> NormalizePhysicalProfileAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        var before = await ReadListingPhysicalProfileAsync(connection, cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.CommandTimeout = 600;
            command.CommandText = "VACUUM (FULL, ANALYZE) public.\"Listings\";";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var after = await ReadListingPhysicalProfileAsync(connection, cancellationToken);
        return new ListingPhysicalNormalizationResult(before, after);
    }

    private static async Task<ListingPhysicalProfile> ReadListingPhysicalProfileAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT pg_relation_size('public."Listings"'::regclass),
                   ceil(pg_relation_size('public."Listings"'::regclass) / 8192.0)::bigint;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new ProfileInvariantException(
                "PostgreSQL returned no Listings physical-profile measurement.");
        }

        return new ListingPhysicalProfile(reader.GetInt64(0), reader.GetInt64(1));
    }

    private static async Task EnsurePublicationIntegritySchemaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = PublicationIntegritySchemaSql;

        var isInstalledAndEnabled =
            (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);

        if (!isInstalledAndEnabled)
        {
            throw new ProfileInvariantException(
                "The query-review profile requires all Chapter 13A translation checks and " +
                "enabled Chapter 13F Active-integrity triggers.");
        }
    }

    private static Task EnsureIntendedActiveAggregatesReadyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        return EnsureNoAggregateViolationsAsync(
            connection,
            transaction,
            IntendedActiveAggregateValidationSql,
            "Intended Active query-review aggregates are not publication-ready",
            cancellationToken);
    }

    private static Task EnsureFinalActiveAggregatesValidAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        return EnsureNoAggregateViolationsAsync(
            connection,
            transaction,
            FinalActiveAggregateValidationSql,
            "Final Active query-review aggregates violate publication integrity",
            cancellationToken);
    }

    private static async Task EnsureNoAggregateViolationsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        string failurePrefix,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 300;
        command.CommandText = sql;

        object? result = await command.ExecuteScalarAsync(cancellationToken);
        string? diagnostics = result as string;

        if (diagnostics is not null)
        {
            throw new ProfileInvariantException(
                $"{failurePrefix}: {diagnostics}.");
        }
    }

    private static async Task EnsureProfileTablesAreEmptyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                (SELECT count(*) FROM "Users") +
                (SELECT count(*) FROM "Agencies") +
                (SELECT count(*) FROM "AgencyMembers") +
                (SELECT count(*) FROM "AgencyInvitations") +
                (SELECT count(*) FROM "Listings") +
                (SELECT count(*) FROM "ListingTranslations") +
                (SELECT count(*) FROM "ListingImages") +
                (SELECT count(*) FROM "ListingApartmentDetails") +
                (SELECT count(*) FROM "ListingHouseDetails");
            """;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        var existingRowCount = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);

        if (existingRowCount != 0)
        {
            throw new ProfileInvariantException(
                "Profile creation requires a fresh disposable database after migrations. " +
                $"The profile tables already contain {existingRowCount:N0} row(s).");
        }
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 600;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private const string PublicationIntegritySchemaSql = """
        WITH expected_triggers("Name", "TableName") AS
        (
            VALUES
                ('TR_Listings_ActivePublicationIntegrity_Insert', 'Listings'),
                ('TR_Listings_ActivePublicationIntegrity_Update', 'Listings'),
                ('TR_ListingTranslations_ActiveFreeze_Insert', 'ListingTranslations'),
                ('TR_ListingTranslations_ActiveFreeze_Update', 'ListingTranslations'),
                ('TR_ListingTranslations_ActiveFreeze_Delete', 'ListingTranslations')
        ),
        installed_triggers AS
        (
            SELECT trigger.tgenabled
            FROM pg_catalog.pg_trigger AS trigger
            JOIN pg_catalog.pg_class AS relation
              ON relation.oid = trigger.tgrelid
            JOIN pg_catalog.pg_namespace AS schema
              ON schema.oid = relation.relnamespace
            JOIN expected_triggers AS expected
              ON expected."Name" = trigger.tgname
             AND expected."TableName" = relation.relname
            WHERE schema.nspname = 'public'
              AND NOT trigger.tgisinternal
        ),
        expected_constraints("Name") AS
        (
            VALUES
                ('CK_ListingTranslations_City_TrimmedNonBlank'),
                ('CK_ListingTranslations_Description_TrimmedNonBlank'),
                ('CK_ListingTranslations_LanguageCode_Canonical'),
                ('CK_ListingTranslations_Title_TrimmedNonBlank')
        ),
        installed_constraints AS
        (
            SELECT db_constraint.convalidated
            FROM pg_catalog.pg_constraint AS db_constraint
            JOIN pg_catalog.pg_class AS relation
              ON relation.oid = db_constraint.conrelid
            JOIN pg_catalog.pg_namespace AS schema
              ON schema.oid = relation.relnamespace
            JOIN expected_constraints AS expected
              ON expected."Name" = db_constraint.conname
            WHERE schema.nspname = 'public'
              AND relation.relname = 'ListingTranslations'
              AND db_constraint.contype = 'c'
        )
        SELECT
            (SELECT count(*) = 5
                    AND bool_and(tgenabled IN ('O', 'A'))
             FROM installed_triggers)
            AND
            (SELECT count(*) = 4
                    AND bool_and(convalidated)
             FROM installed_constraints);
        """;

    private const string IntendedActiveAggregateValidationSql = """
        WITH invalid AS
        (
            SELECT staged."ListingId",
                   count(translation."Id") AS translation_count,
                   count(translation."Id") FILTER
                       (WHERE translation."City" IS NULL) AS null_city_count,
                   count(translation."Id") FILTER
                       (WHERE translation."Description" IS NULL)
                       AS null_description_count
            FROM pg_temp."QueryReviewIntendedListingStatuses" AS staged
            LEFT JOIN public."ListingTranslations" AS translation
              ON translation."ListingId" = staged."ListingId"
            WHERE staged."IntendedStatus" = 'Active'
            GROUP BY staged."ListingId"
            HAVING count(translation."Id") = 0
                OR count(translation."Id") FILTER
                    (WHERE translation."City" IS NULL) > 0
                OR count(translation."Id") FILTER
                    (WHERE translation."Description" IS NULL) > 0
            ORDER BY staged."ListingId"
            LIMIT 10
        )
        SELECT string_agg(
            format(
                '%s translations=%s null_city=%s null_description=%s',
                "ListingId",
                translation_count,
                null_city_count,
                null_description_count),
            '; ' ORDER BY "ListingId")
        FROM invalid;
        """;

    private const string FinalActiveAggregateValidationSql = """
        WITH invalid AS
        (
            SELECT listing."Id" AS "ListingId",
                   count(translation."Id") AS translation_count,
                   count(translation."Id") FILTER
                       (WHERE translation."City" IS NULL) AS null_city_count,
                   count(translation."Id") FILTER
                       (WHERE translation."Description" IS NULL)
                       AS null_description_count
            FROM public."Listings" AS listing
            LEFT JOIN public."ListingTranslations" AS translation
              ON translation."ListingId" = listing."Id"
            WHERE listing."Status" = 'Active'
            GROUP BY listing."Id"
            HAVING count(translation."Id") = 0
                OR count(translation."Id") FILTER
                    (WHERE translation."City" IS NULL) > 0
                OR count(translation."Id") FILTER
                    (WHERE translation."Description" IS NULL) > 0
            ORDER BY listing."Id"
            LIMIT 10
        )
        SELECT string_agg(
            format(
                '%s translations=%s null_city=%s null_description=%s',
                "ListingId",
                translation_count,
                null_city_count,
                null_description_count),
            '; ' ORDER BY "ListingId")
        FROM invalid;
        """;

    private const string SeedUsersAndAgenciesSql = """
        INSERT INTO "Users"
        (
            "Id", "Email", "NormalizedEmail", "PasswordHash", "FirstName", "LastName",
            "PhoneNumber", "Role", "Status", "AvatarUrl", "AvatarStoredFileName",
            "AvatarContentType", "AvatarSizeBytes", "CreatedAtUtc", "ModifiedAtUtc"
        )
        SELECT ('10000000-0000-0000-0000-' || lpad(to_hex(i), 12, '0'))::uuid,
               format('queryreview-user-%s@example.invalid', lpad(i::text, 3, '0')),
               upper(format('queryreview-user-%s@example.invalid', lpad(i::text, 3, '0'))),
               'query-review-profile-not-a-login',
               'QueryReview',
               format('User%s', lpad(i::text, 3, '0')),
               NULL,
               CASE WHEN i <= 100 THEN 'AgencyOwner' ELSE 'User' END,
               'Active',
               NULL,
               NULL,
               NULL,
               NULL,
               '2026-01-01T00:00:00Z'::timestamptz,
               NULL
        FROM generate_series(1, 101) AS series(i);

        INSERT INTO "Agencies"
        (
            "Id", "Name", "Slug", "Description", "LogoUrl", "LogoStoredFileName",
            "LogoContentType", "LogoSizeBytes", "PhoneNumber", "Email", "WebsiteUrl",
            "AddressLine", "City", "Municipality", "Status", "CreatedAtUtc", "ModifiedAtUtc"
        )
        SELECT ('20000000-0000-0000-0000-' || lpad(to_hex(i), 12, '0'))::uuid,
               format('Query Review Agency %s', lpad(i::text, 3, '0')),
               format('query-review-agency-%s', lpad(i::text, 3, '0')),
               'Deterministic Chapter 10F benchmark agency',
               NULL,
               NULL,
               NULL,
               NULL,
               NULL,
               format('queryreview-agency-%s@example.invalid', lpad(i::text, 3, '0')),
               NULL,
               NULL,
               'Skopje',
               'Centar',
               'Active',
               '2026-01-01T00:00:00Z'::timestamptz,
               NULL
        FROM generate_series(1, 100) AS series(i);

        INSERT INTO "AgencyMembers"
        (
            "Id", "AgencyId", "UserId", "Role", "Status", "CreatedAtUtc", "ModifiedAtUtc"
        )
        SELECT ('30000000-0000-0000-0000-' || lpad(to_hex(i), 12, '0'))::uuid,
               ('20000000-0000-0000-0000-' || lpad(to_hex(i), 12, '0'))::uuid,
               ('10000000-0000-0000-0000-' || lpad(to_hex(i), 12, '0'))::uuid,
               'Owner',
               'Active',
               '2026-01-01T00:00:00Z'::timestamptz,
               NULL
        FROM generate_series(1, 100) AS series(i);
        """;

    private const string SeedListingsSql = """
        CREATE TEMP TABLE "QueryReviewIntendedListingStatuses"
        ON COMMIT DROP
        AS
        SELECT i AS "Sequence",
               ('40000000-0000-0000-0000-' ||
                lpad(to_hex(i), 12, '0'))::uuid AS "ListingId",
               CASE
                   WHEN i BETWEEN 1 AND 70000 THEN 'Active'
                   WHEN i BETWEEN 70001 AND 76000 THEN 'Draft'
                   WHEN i BETWEEN 76001 AND 82000 THEN 'Archived'
                   WHEN i BETWEEN 82001 AND 88000 THEN 'Reserved'
                   WHEN i BETWEEN 88001 AND 94000 THEN 'Sold'
                   ELSE 'Rented'
               END AS "IntendedStatus"
        FROM generate_series(1, 100000) AS series(i);

        WITH source AS
        (
            SELECT staged."Sequence" AS i,
                   staged."ListingId" AS listing_id,
                   CASE
                       WHEN staged."Sequence" BETWEEN 3001 AND 3031 THEN 'Rent'
                       WHEN staged."Sequence" BETWEEN 3033 AND 3061
                            AND mod(staged."Sequence", 2) = 1 THEN 'Sale'
                       WHEN mod(staged."Sequence", 2) = 0 THEN 'Sale'
                       ELSE 'Rent'
                   END AS listing_type,
                   CASE
                       WHEN staged."Sequence" BETWEEN 3001 AND 3031 THEN 'Apartment'
                       WHEN staged."Sequence" BETWEEN 3101 AND 3129
                            AND mod(staged."Sequence", 4) IN (1, 2) THEN 'House'
                       WHEN mod(((staged."Sequence" - 1) / 2), 2) = 0
                           THEN 'Apartment'
                       ELSE 'House'
                   END AS property_type,
                   CASE
                       WHEN staged."Sequence" BETWEEN 3001 AND 3031 THEN 'EUR'
                       WHEN staged."Sequence" BETWEEN 3202 AND 3229
                            AND mod(staged."Sequence", 3) = 1 THEN 'USD'
                       WHEN staged."Sequence" BETWEEN 3232 AND 3259
                            AND mod(staged."Sequence", 3) = 1 THEN 'MKD'
                       WHEN mod(staged."Sequence", 3) = 1 THEN 'EUR'
                       WHEN mod(staged."Sequence", 3) = 2 THEN 'USD'
                       ELSE 'MKD'
                   END AS currency
            FROM pg_temp."QueryReviewIntendedListingStatuses" AS staged
        )
        INSERT INTO "Listings"
        (
            "Id", "CreatedByUserId", "AgencyId", "ListingType", "PropertyType", "Status",
            "Price", "Currency", "AreaSquareMeters", "Rooms", "Bathrooms", "BalconyCount",
            "ParkingSpaces", "HasBasement", "IsExchangePossible", "HeatingType",
            "FurnishingStatus", "Condition", "YearRenovated", "Orientation", "YearBuilt",
            "Latitude", "Longitude", "CreatedAtUtc", "ModifiedAtUtc"
        )
        SELECT listing_id,
               CASE
                   WHEN mod(i, 2) = 0
                       THEN '10000000-0000-0000-0000-000000000065'::uuid
                   ELSE ('10000000-0000-0000-0000-' ||
                         lpad(to_hex(mod(((i - 1) / 2), 100) + 1), 12, '0'))::uuid
               END,
               CASE
                   WHEN mod(i, 2) = 0 THEN NULL
                   ELSE ('20000000-0000-0000-0000-' ||
                         lpad(to_hex(mod(((i - 1) / 2), 100) + 1), 12, '0'))::uuid
               END,
               listing_type,
               property_type,
               'Draft',
               CASE
                   WHEN i = 3001 THEN 200000
                   WHEN i BETWEEN 3002 AND 3031 THEN
                       CASE mod(i - 3002, 10)
                           WHEN 0 THEN 200000
                           WHEN 1 THEN 200000
                           WHEN 2 THEN 202000
                           WHEN 3 THEN 198000
                           WHEN 4 THEN 210000
                           WHEN 5 THEN 207900
                           WHEN 6 THEN 220000
                           WHEN 7 THEN 217800
                           WHEN 8 THEN 240000
                           ELSE 237600
                       END
                   ELSE 50000 + 2500 * mod(i, 120)
               END,
               currency,
               CASE
                   WHEN i = 3001 THEN 100
                   WHEN i BETWEEN 3002 AND 3031 THEN
                       CASE mod(i - 3002, 10)
                           WHEN 0 THEN 100
                           WHEN 1 THEN 100
                           WHEN 2 THEN 100
                           WHEN 3 THEN 100
                           WHEN 4 THEN 105
                           WHEN 5 THEN 105
                           WHEN 6 THEN 110
                           WHEN 7 THEN 110
                           WHEN 8 THEN 120
                           ELSE 120
                       END
                   ELSE 40 + mod(i, 200)
               END,
               CASE WHEN mod(i, 5) = 0 THEN NULL ELSE 1.0 + 0.5 * mod(i, 8) END,
               1.0 + 0.5 * mod(i, 4),
               mod(i, 3),
               mod(i, 2),
               mod(i, 2) = 0,
               mod(i, 10) = 0,
               CASE WHEN mod(i, 2) = 0 THEN 'Central' ELSE 'Electric' END,
               CASE WHEN mod(i, 3) = 0 THEN 'Furnished' ELSE 'Unfurnished' END,
               CASE WHEN mod(i, 4) = 0 THEN 'Renovated' ELSE 'Good' END,
               CASE WHEN mod(i, 10) = 0 THEN 2015 + mod(i, 10) ELSE NULL END,
               CASE WHEN mod(i, 2) = 0 THEN 'South' ELSE 'North' END,
               1950 + mod(i, 75),
               CASE WHEN mod(i, 5) = 0 THEN NULL ELSE 41.000000 + mod(i, 1000) * 0.000001 END,
               CASE WHEN mod(i, 5) = 0 THEN NULL ELSE 21.000000 + mod(i, 1000) * 0.000001 END,
               CASE
                   WHEN i BETWEEN 3001 AND 3031 THEN '2026-02-01T00:00:00Z'::timestamptz
                   ELSE '2026-01-01T00:00:00Z'::timestamptz + make_interval(mins => mod(i, 1000))
               END,
               NULL
        FROM source;
        """;

    private const string ApplyIntendedStatusesSql = """
        UPDATE public."Listings" AS listing
        SET "Status" = staged."IntendedStatus"
        FROM pg_temp."QueryReviewIntendedListingStatuses" AS staged
        WHERE listing."Id" = staged."ListingId"
          AND listing."Status" IS DISTINCT FROM staged."IntendedStatus";
        """;

    private const string SeedTranslationsSql = """
        WITH translation_source AS
        (
            SELECT i,
                   slot,
                   CASE
                       WHEN slot = 2 THEN 'mk'
                       WHEN i BETWEEN 5001 AND 10000 THEN 'de'
                       WHEN i BETWEEN 10001 AND 15000 THEN 'sq'
                       ELSE 'en'
                   END AS language_code
            FROM generate_series(1, 100000) AS listings(i)
            CROSS JOIN generate_series(1, 2) AS slots(slot)
        ),
        shaped AS
        (
            SELECT i,
                   slot,
                   language_code,
                   CASE
                       WHEN i BETWEEN 2001 AND 2120
                           THEN format('Benchmark broadtoken10f needle10f listing %s %s', i, language_code)
                       WHEN i <= 10000
                           THEN format('Benchmark broadtoken10f listing %s %s', i, language_code)
                       ELSE format('Benchmark listing %s %s', i, language_code)
                   END AS title,
                   CASE
                       WHEN i BETWEEN 15001 AND 15250 THEN 'needle10f description decoy'
                       ELSE format('Deterministic benchmark description %s', i)
                   END AS description,
                   CASE
                       WHEN i BETWEEN 15251 AND 15500 THEN 'needle10f address decoy'
                       ELSE format('Benchmark address %s', i)
                   END AS address_line,
                   CASE
                       WHEN i BETWEEN 1001 AND 1140 THEN 'AuditCity10F'
                       WHEN i BETWEEN 3001 AND 3031 THEN 'ComparableCity10F'
                       WHEN i <= 28171 THEN 'Skopje'
                       WHEN i <= 42171 THEN 'Bitola'
                       WHEN i <= 70000 THEN format('BenchmarkCity%s', lpad((mod(i - 42172, 28) + 1)::text, 2, '0'))
                       ELSE format('HiddenCity%s', lpad((mod(i - 70001, 12) + 1)::text, 2, '0'))
                   END AS city,
                   CASE
                       WHEN i BETWEEN 1001 AND 1140 THEN 'AuditMunicipality10F'
                       WHEN i BETWEEN 3001 AND 3021 THEN 'ComparableMunicipality10F'
                       WHEN i BETWEEN 3022 AND 3031 THEN 'ComparableMunicipalityOther10F'
                       WHEN i <= 28171 THEN 'Centar'
                       WHEN i <= 42171 THEN 'Bitola'
                       WHEN i <= 70000 THEN format('BenchmarkMunicipality%s', lpad((mod(i - 42172, 28) + 1)::text, 2, '0'))
                       ELSE 'HiddenMunicipality'
                   END AS municipality,
                   CASE
                       WHEN i BETWEEN 1001 AND 1140 THEN 'AuditNeighborhood10F'
                       WHEN i BETWEEN 3001 AND 3011 THEN 'ComparableNeighborhood10F'
                       WHEN i BETWEEN 3012 AND 3021 THEN 'ComparableNeighborhoodOther10F'
                       WHEN i BETWEEN 3022 AND 3031 THEN 'ComparableNeighborhoodOther10F'
                       WHEN i <= 28171 THEN 'Center'
                       WHEN i <= 42171 THEN 'Center'
                       WHEN i <= 70000 THEN format('BenchmarkNeighborhood%s', lpad((mod(i - 42172, 28) + 1)::text, 2, '0'))
                       ELSE 'HiddenNeighborhood'
                   END AS neighborhood
            FROM translation_source
        )
        INSERT INTO "ListingTranslations"
        (
            "Id", "ListingId", "LanguageCode", "Title", "Description", "AddressLine",
            "City", "Municipality", "Neighborhood"
        )
        SELECT ((CASE WHEN slot = 1 THEN '50000000' ELSE '60000000' END) ||
                '-0000-0000-0000-' || lpad(to_hex(i), 12, '0'))::uuid,
               ('40000000-0000-0000-0000-' || lpad(to_hex(i), 12, '0'))::uuid,
               language_code,
               title,
               description,
               address_line,
               city,
               municipality,
               neighborhood
        FROM shaped;
        """;

    private const string SeedDetailsSql = """
        INSERT INTO "ListingApartmentDetails"
        (
            "ListingId", "ApartmentType", "Floor", "TotalFloors", "HasElevator"
        )
        SELECT l."Id",
               'Standard',
               mod(i, 10),
               10,
               mod(i, 2) = 0
        FROM generate_series(1, 100000) AS series(i)
        JOIN "Listings" AS l
          ON l."Id" = ('40000000-0000-0000-0000-' || lpad(to_hex(i), 12, '0'))::uuid
        WHERE l."PropertyType" = 'Apartment';

        INSERT INTO "ListingHouseDetails"
        (
            "ListingId", "HouseType", "NumberOfFloors", "YardAreaSquareMeters"
        )
        SELECT l."Id",
               'Detached',
               1 + mod(i, 3),
               100 + mod(i, 400)
        FROM generate_series(1, 100000) AS series(i)
        JOIN "Listings" AS l
          ON l."Id" = ('40000000-0000-0000-0000-' || lpad(to_hex(i), 12, '0'))::uuid
        WHERE l."PropertyType" = 'House';
        """;

    private const string SeedImagesSql = """
        INSERT INTO "ListingImages"
        (
            "Id", "ListingId", "OriginalFileName", "StoredFileName", "ContentType",
            "SizeBytes", "Url", "SortOrder", "IsPrimary", "CreatedAtUtc", "ModifiedAtUtc"
        )
        SELECT ('70000000-0000-0000-0000-' || lpad(to_hex(i), 12, '0'))::uuid,
               ('40000000-0000-0000-0000-' || lpad(to_hex(i), 12, '0'))::uuid,
               format('listing-%s-primary.jpg', i),
               format('queryreview-%s-primary.jpg', i),
               'image/jpeg',
               100000 + i,
               format('https://queryreview.invalid/listings/%s/primary.jpg', i),
               0,
               TRUE,
               '2026-01-01T00:00:00Z'::timestamptz,
               NULL
        FROM generate_series(2, 100000, 2) AS series(i);

        INSERT INTO "ListingImages"
        (
            "Id", "ListingId", "OriginalFileName", "StoredFileName", "ContentType",
            "SizeBytes", "Url", "SortOrder", "IsPrimary", "CreatedAtUtc", "ModifiedAtUtc"
        )
        SELECT ('71000000-0000-0000-0000-' || lpad(to_hex(i), 12, '0'))::uuid,
               ('40000000-0000-0000-0000-' || lpad(to_hex(i), 12, '0'))::uuid,
               format('listing-%s-secondary.jpg', i),
               format('queryreview-%s-secondary.jpg', i),
               'image/jpeg',
               90000 + i,
               format('https://queryreview.invalid/listings/%s/secondary.jpg', i),
               1,
               FALSE,
               '2026-01-01T00:00:00Z'::timestamptz,
               NULL
        FROM generate_series(10, 100000, 10) AS series(i);
        """;
}
