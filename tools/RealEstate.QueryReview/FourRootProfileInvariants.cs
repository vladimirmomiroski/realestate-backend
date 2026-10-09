using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace RealEstate.QueryReview;

internal sealed record FourRootProfileIdentity(
    int InvariantCount,
    string ProfileSha256,
    string InvariantManifestSha256,
    string InvariantResultSha256);

internal static class FourRootProfileInvariants
{
    private static readonly string[] Statuses =
        ["Active", "Draft", "Archived", "Reserved", "Sold", "Rented"];

    private static readonly IReadOnlyDictionary<string, long> Expected = BuildExpected();

    public static int InvariantCount => Expected.Count;

    public static IReadOnlyDictionary<string, long> ExpectedValues => Expected;

    public static string InvariantManifestSha256 => ComputeInvariantHash(
        Expected.Select(item => $"{item.Key}={item.Value}"));

    public static async Task<ProfileVerificationResult> VerifyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 300;
        command.CommandText = VerificationSql;

        var actual = new Dictionary<string, long>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            actual.Add(reader.GetString(0), reader.GetInt64(1));
        }

        var missing = Expected.Keys.Except(actual.Keys, StringComparer.Ordinal).ToArray();
        var unexpected = actual.Keys.Except(Expected.Keys, StringComparer.Ordinal).ToArray();

        if (missing.Length > 0 || unexpected.Length > 0)
        {
            throw new ProfileInvariantException(
                "The four-root-discovery-v1 invariant query returned an unexpected metric set. " +
                $"Missing: [{string.Join(", ", missing)}]. " +
                $"Unexpected: [{string.Join(", ", unexpected)}].");
        }

        return new ProfileVerificationResult(
            Expected
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new ProfileInvariant(entry.Key, entry.Value, actual[entry.Key]))
                .ToArray(),
            QueryReviewGenerations.FourRootDiscoveryId);
    }

    public static async Task<FourRootProfileIdentity> ComputeIdentityAsync(
        NpgsqlConnection connection,
        ProfileVerificationResult verification,
        CancellationToken cancellationToken = default)
    {
        string profileHash = await ComputeProfileHashAsync(connection, cancellationToken);
        string manifestHash = InvariantManifestSha256;
        string resultHash = ComputeInvariantHash(
            verification.Invariants.Select(item => $"{item.Name}={item.Actual}"));

        return new FourRootProfileIdentity(
            verification.Invariants.Count,
            profileHash,
            manifestHash,
            resultHash);
    }

    private static IReadOnlyDictionary<string, long> BuildExpected()
    {
        var expected = new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["users.total"] = 101,
            ["agencies.total"] = 100,
            ["agency_members.total"] = 100,
            ["listings.total"] = 100_000,
            ["listings.active"] = 70_000,
            ["listings.draft"] = 6_000,
            ["listings.archived"] = 6_000,
            ["listings.reserved"] = 6_000,
            ["listings.sold"] = 6_000,
            ["listings.rented"] = 6_000,
            ["ownership.personal"] = 50_000,
            ["ownership.agency"] = 50_000,
            ["ownership.common_agencies_exact"] = 8,
            ["ownership.sparse_agencies_exact"] = 19,
            ["ownership.legacy_agency_one_total"] = 500,
            ["ownership.legacy_agency_one_active"] = 350,
            ["property_type.apartment"] = 40_000,
            ["property_type.house"] = 30_000,
            ["property_type.commercial"] = 20_000,
            ["property_type.land"] = 10_000,
            ["commercial_type.unknown"] = 8_000,
            ["commercial_type.office"] = 6_000,
            ["commercial_type.shop"] = 4_000,
            ["commercial_type.other"] = 2_000,
            ["land_type.unknown"] = 4_000,
            ["land_type.buildingplot"] = 3_000,
            ["land_type.agriculturalland"] = 2_000,
            ["land_type.other"] = 1_000,
            ["listing_type.sale"] = 50_000,
            ["listing_type.rent"] = 50_000,
            ["currency.eur"] = 33_334,
            ["currency.usd"] = 33_333,
            ["currency.mkd"] = 33_333,
            ["currency.root_subtype_missing_combinations"] = 0,
            ["translations.total"] = 200_000,
            ["translations.exactly_two_per_listing"] = 100_000,
            ["translations.canonical_violations"] = 0,
            ["images.total"] = 60_000,
            ["images.zero"] = 50_000,
            ["images.one"] = 40_000,
            ["images.multiple"] = 10_000,
            ["details.apartment"] = 40_000,
            ["details.house"] = 30_000,
            ["details.commercial"] = 20_000,
            ["details.land"] = 10_000,
            ["details.exactly_one_matching"] = 100_000,
            ["details.missing_matching"] = 0,
            ["details.mismatched_or_extra"] = 0,
            ["locations.selective"] = 140,
            ["locations.skopje"] = 28_000,
            ["locations.bitola"] = 14_000,
            ["text.selective_q"] = 120,
            ["text.broad_title"] = 10_000,
            ["text.excluded_decoys"] = 500,
            ["comparables.cluster"] = 31,
            ["comparables.eligible_candidates"] = 30,
            ["comparables.protected_root_and_type"] = 31,
            ["protected.legacy_property_type_violations"] = 0,
            ["distribution.sequence_windows_with_all_roots"] = 100
        };

        AddBandExpected(expected, "root.apartment", 40_000);
        AddBandExpected(expected, "root.house", 30_000);
        AddBandExpected(expected, "root.commercial", 20_000);
        AddBandExpected(expected, "root.land", 10_000);
        AddBandExpected(expected, "commercial.unknown", 8_000);
        AddBandExpected(expected, "commercial.office", 6_000);
        AddBandExpected(expected, "commercial.shop", 4_000);
        AddBandExpected(expected, "commercial.other", 2_000);
        AddBandExpected(expected, "land.unknown", 4_000);
        AddBandExpected(expected, "land.buildingplot", 3_000);
        AddBandExpected(expected, "land.agriculturalland", 2_000);
        AddBandExpected(expected, "land.other", 1_000);

        return expected;
    }

    private static void AddBandExpected(
        IDictionary<string, long> expected,
        string prefix,
        long total)
    {
        expected[$"{prefix}.status.active"] = total * 70 / 100;
        foreach (string status in Statuses.Skip(1))
        {
            expected[$"{prefix}.status.{status.ToLowerInvariant()}"] = total * 6 / 100;
        }

        expected[$"{prefix}.ownership.personal"] = total / 2;
        expected[$"{prefix}.ownership.agency"] = total / 2;
        expected[$"{prefix}.listing_type.sale"] = total / 2;
        expected[$"{prefix}.listing_type.rent"] = total / 2;
    }

    private static async Task<string> ComputeProfileHashAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 300;
        command.CommandText = ProfileIdentitySql;
        await using var reader = await command.ExecuteReaderAsync(
            System.Data.CommandBehavior.SequentialAccess,
            cancellationToken);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        while (await reader.ReadAsync(cancellationToken))
        {
            string line = $"{reader.GetString(0)}|{reader.GetString(1)}|{reader.GetString(2)}\n";
            hash.AppendData(Encoding.UTF8.GetBytes(line));
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string ComputeInvariantHash(IEnumerable<string> values)
    {
        string canonical = string.Join(
            '\n',
            values.OrderBy(value => value, StringComparer.Ordinal)) + "\n";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    private const string VerificationSql = """
        WITH metrics("Name", "Actual") AS
        (
            SELECT 'users.total', count(*) FROM "Users"
            UNION ALL SELECT 'agencies.total', count(*) FROM "Agencies"
            UNION ALL SELECT 'agency_members.total', count(*) FROM "AgencyMembers"
            UNION ALL SELECT 'listings.total', count(*) FROM "Listings"
            UNION ALL SELECT 'listings.active', count(*) FROM "Listings" WHERE "Status" = 'Active'
            UNION ALL SELECT 'listings.draft', count(*) FROM "Listings" WHERE "Status" = 'Draft'
            UNION ALL SELECT 'listings.archived', count(*) FROM "Listings" WHERE "Status" = 'Archived'
            UNION ALL SELECT 'listings.reserved', count(*) FROM "Listings" WHERE "Status" = 'Reserved'
            UNION ALL SELECT 'listings.sold', count(*) FROM "Listings" WHERE "Status" = 'Sold'
            UNION ALL SELECT 'listings.rented', count(*) FROM "Listings" WHERE "Status" = 'Rented'
            UNION ALL SELECT 'ownership.personal', count(*) FROM "Listings" WHERE "AgencyId" IS NULL
            UNION ALL SELECT 'ownership.agency', count(*) FROM "Listings" WHERE "AgencyId" IS NOT NULL
            UNION ALL SELECT 'ownership.common_agencies_exact', count(*) FROM
                (SELECT "AgencyId" FROM "Listings"
                 WHERE "AgencyId" BETWEEN '20000000-0000-0000-0000-000000000002'::uuid
                                      AND '20000000-0000-0000-0000-000000000009'::uuid
                 GROUP BY "AgencyId" HAVING count(*) = 5000) AS value
            UNION ALL SELECT 'ownership.sparse_agencies_exact', count(*) FROM
                (SELECT "AgencyId" FROM "Listings"
                 WHERE "AgencyId" BETWEEN '20000000-0000-0000-0000-00000000000a'::uuid
                                      AND '20000000-0000-0000-0000-00000000001c'::uuid
                 GROUP BY "AgencyId" HAVING count(*) = 500) AS value
            UNION ALL SELECT 'ownership.legacy_agency_one_total', count(*) FROM "Listings"
                WHERE "AgencyId" = '20000000-0000-0000-0000-000000000001'::uuid
            UNION ALL SELECT 'ownership.legacy_agency_one_active', count(*) FROM "Listings"
                WHERE "AgencyId" = '20000000-0000-0000-0000-000000000001'::uuid AND "Status" = 'Active'
            UNION ALL SELECT 'property_type.' || lower("PropertyType"), count(*) FROM "Listings" GROUP BY "PropertyType"
            UNION ALL SELECT 'commercial_type.' || lower("CommercialType"), count(*) FROM "ListingCommercialDetails" GROUP BY "CommercialType"
            UNION ALL SELECT 'land_type.' || lower("LandType"), count(*) FROM "ListingLandDetails" GROUP BY "LandType"
            UNION ALL SELECT 'listing_type.' || lower("ListingType"), count(*) FROM "Listings" GROUP BY "ListingType"
            UNION ALL SELECT 'currency.' || lower("Currency"), count(*) FROM "Listings" GROUP BY "Currency"
            UNION ALL SELECT 'currency.root_subtype_missing_combinations', count(*) FROM
            (
                SELECT band, currency
                FROM (VALUES
                    ('root.apartment'), ('root.house'), ('root.commercial'), ('root.land'),
                    ('commercial.unknown'), ('commercial.office'), ('commercial.shop'), ('commercial.other'),
                    ('land.unknown'), ('land.buildingplot'), ('land.agriculturalland'), ('land.other')) AS bands(band)
                CROSS JOIN (VALUES ('EUR'), ('USD'), ('MKD')) AS currencies(currency)
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM "Listings" AS listing
                    LEFT JOIN "ListingCommercialDetails" AS commercial ON commercial."ListingId" = listing."Id"
                    LEFT JOIN "ListingLandDetails" AS land ON land."ListingId" = listing."Id"
                    WHERE listing."Currency" = currencies.currency
                      AND CASE
                          WHEN bands.band = 'root.' || lower(listing."PropertyType") THEN true
                          WHEN bands.band = 'commercial.' || lower(commercial."CommercialType") THEN listing."PropertyType" = 'Commercial'
                          WHEN bands.band = 'land.' || lower(land."LandType") THEN listing."PropertyType" = 'Land'
                          ELSE false
                      END
                )
            ) AS missing_currency
            UNION ALL SELECT 'translations.total', count(*) FROM "ListingTranslations"
            UNION ALL SELECT 'translations.exactly_two_per_listing', count(*) FROM
                (SELECT "ListingId" FROM "ListingTranslations" GROUP BY "ListingId" HAVING count(*) = 2) AS value
            UNION ALL SELECT 'translations.canonical_violations', count(*) FROM "ListingTranslations"
                WHERE "LanguageCode" <> lower(btrim("LanguageCode")) OR "LanguageCode" NOT IN ('en','mk','de','sq')
            UNION ALL SELECT 'images.total', count(*) FROM "ListingImages"
            UNION ALL SELECT 'images.zero', count(*) FROM "Listings" AS listing
                WHERE NOT EXISTS (SELECT 1 FROM "ListingImages" AS image WHERE image."ListingId" = listing."Id")
            UNION ALL SELECT 'images.one', count(*) FROM
                (SELECT "ListingId" FROM "ListingImages" GROUP BY "ListingId" HAVING count(*) = 1) AS value
            UNION ALL SELECT 'images.multiple', count(*) FROM
                (SELECT "ListingId" FROM "ListingImages" GROUP BY "ListingId" HAVING count(*) > 1) AS value
            UNION ALL SELECT 'details.apartment', count(*) FROM "ListingApartmentDetails"
            UNION ALL SELECT 'details.house', count(*) FROM "ListingHouseDetails"
            UNION ALL SELECT 'details.commercial', count(*) FROM "ListingCommercialDetails"
            UNION ALL SELECT 'details.land', count(*) FROM "ListingLandDetails"
            UNION ALL SELECT 'details.exactly_one_matching', count(*) FROM "Listings" AS listing
                LEFT JOIN "ListingApartmentDetails" AS apartment ON apartment."ListingId" = listing."Id"
                LEFT JOIN "ListingHouseDetails" AS house ON house."ListingId" = listing."Id"
                LEFT JOIN "ListingCommercialDetails" AS commercial ON commercial."ListingId" = listing."Id"
                LEFT JOIN "ListingLandDetails" AS land ON land."ListingId" = listing."Id"
                WHERE (CASE WHEN listing."PropertyType" = 'Apartment' AND apartment."ListingId" IS NOT NULL THEN 1 ELSE 0 END +
                       CASE WHEN listing."PropertyType" = 'House' AND house."ListingId" IS NOT NULL THEN 1 ELSE 0 END +
                       CASE WHEN listing."PropertyType" = 'Commercial' AND commercial."ListingId" IS NOT NULL THEN 1 ELSE 0 END +
                       CASE WHEN listing."PropertyType" = 'Land' AND land."ListingId" IS NOT NULL THEN 1 ELSE 0 END) = 1
                  AND (CASE WHEN apartment."ListingId" IS NOT NULL THEN 1 ELSE 0 END +
                       CASE WHEN house."ListingId" IS NOT NULL THEN 1 ELSE 0 END +
                       CASE WHEN commercial."ListingId" IS NOT NULL THEN 1 ELSE 0 END +
                       CASE WHEN land."ListingId" IS NOT NULL THEN 1 ELSE 0 END) = 1
            UNION ALL SELECT 'details.missing_matching', count(*) FROM "Listings" AS listing
                LEFT JOIN "ListingApartmentDetails" AS apartment ON apartment."ListingId" = listing."Id"
                LEFT JOIN "ListingHouseDetails" AS house ON house."ListingId" = listing."Id"
                LEFT JOIN "ListingCommercialDetails" AS commercial ON commercial."ListingId" = listing."Id"
                LEFT JOIN "ListingLandDetails" AS land ON land."ListingId" = listing."Id"
                WHERE CASE listing."PropertyType"
                    WHEN 'Apartment' THEN apartment."ListingId" IS NULL
                    WHEN 'House' THEN house."ListingId" IS NULL
                    WHEN 'Commercial' THEN commercial."ListingId" IS NULL
                    WHEN 'Land' THEN land."ListingId" IS NULL
                    ELSE true END
            UNION ALL SELECT 'details.mismatched_or_extra', count(*) FROM "Listings" AS listing
                LEFT JOIN "ListingApartmentDetails" AS apartment ON apartment."ListingId" = listing."Id"
                LEFT JOIN "ListingHouseDetails" AS house ON house."ListingId" = listing."Id"
                LEFT JOIN "ListingCommercialDetails" AS commercial ON commercial."ListingId" = listing."Id"
                LEFT JOIN "ListingLandDetails" AS land ON land."ListingId" = listing."Id"
                WHERE (CASE WHEN apartment."ListingId" IS NOT NULL THEN 1 ELSE 0 END +
                       CASE WHEN house."ListingId" IS NOT NULL THEN 1 ELSE 0 END +
                       CASE WHEN commercial."ListingId" IS NOT NULL THEN 1 ELSE 0 END +
                       CASE WHEN land."ListingId" IS NOT NULL THEN 1 ELSE 0 END) <> 1
                   OR (apartment."ListingId" IS NOT NULL AND listing."PropertyType" <> 'Apartment')
                   OR (house."ListingId" IS NOT NULL AND listing."PropertyType" <> 'House')
                   OR (commercial."ListingId" IS NOT NULL AND listing."PropertyType" <> 'Commercial')
                   OR (land."ListingId" IS NOT NULL AND listing."PropertyType" <> 'Land')
            UNION ALL SELECT 'locations.selective', count(DISTINCT "ListingId") FROM "ListingTranslations" WHERE "City" = 'AuditCity10F'
            UNION ALL SELECT 'locations.skopje', count(DISTINCT "ListingId") FROM "ListingTranslations" WHERE "City" = 'Skopje'
            UNION ALL SELECT 'locations.bitola', count(DISTINCT "ListingId") FROM "ListingTranslations" WHERE "City" = 'Bitola'
            UNION ALL SELECT 'text.selective_q', count(DISTINCT "ListingId") FROM "ListingTranslations" WHERE "Title" LIKE '%needle10f%'
            UNION ALL SELECT 'text.broad_title', count(DISTINCT "ListingId") FROM "ListingTranslations" WHERE "Title" LIKE '%broadtoken10f%'
            UNION ALL SELECT 'text.excluded_decoys', count(DISTINCT "ListingId") FROM "ListingTranslations"
                WHERE "Description" LIKE '%needle10f%' OR "AddressLine" LIKE '%needle10f%'
            UNION ALL SELECT 'comparables.cluster', count(*) FROM "Listings" AS listing
                WHERE listing."Id" BETWEEN '40000000-0000-0000-0000-000000000bb9'::uuid AND '40000000-0000-0000-0000-000000000bd7'::uuid
            UNION ALL SELECT 'comparables.eligible_candidates', count(*) FROM "Listings" AS listing
                WHERE listing."Id" BETWEEN '40000000-0000-0000-0000-000000000bba'::uuid AND '40000000-0000-0000-0000-000000000bd7'::uuid
            UNION ALL SELECT 'comparables.protected_root_and_type', count(*) FROM "Listings" AS listing
                WHERE listing."Id" BETWEEN '40000000-0000-0000-0000-000000000bb9'::uuid AND '40000000-0000-0000-0000-000000000bd7'::uuid
                  AND listing."PropertyType" = 'Apartment' AND listing."ListingType" = 'Rent'
                  AND listing."Currency" = 'EUR' AND listing."Status" = 'Active'
            UNION ALL SELECT 'protected.legacy_property_type_violations', count(*)
            FROM generate_series(1, 100000) AS sequence(i)
            JOIN "Listings" AS listing
              ON listing."Id" = ('40000000-0000-0000-0000-' || lpad(to_hex(sequence.i), 12, '0'))::uuid
            WHERE
                (
                    ((sequence.i + 1) / 2) BETWEEN 501 AND 570
                    OR ((sequence.i + 1) / 2) BETWEEN 1001 AND 1060
                    OR ((sequence.i + 1) / 2) BETWEEN 1501 AND 1516
                    OR (((sequence.i + 1) / 2) BETWEEN 6481 AND 34981 AND mod(((sequence.i + 1) / 2) - 6481, 1500) = 0)
                    OR (((sequence.i + 1) / 2) BETWEEN 5999 AND 34499 AND mod(((sequence.i + 1) / 2) - 5999, 1500) = 0)
                    OR (((sequence.i + 1) / 2) BETWEEN 25901 AND 34901 AND mod(((sequence.i + 1) / 2) - 25901, 500) = 0)
                    OR (((sequence.i + 1) / 2) BETWEEN 25422 AND 34922 AND mod(((sequence.i + 1) / 2) - 25422, 500) = 0)
                )
                AND listing."PropertyType" <> CASE
                    WHEN sequence.i BETWEEN 3001 AND 3032 THEN 'Apartment'
                    WHEN mod((((sequence.i + 1) / 2) - 1), 2) = 0 THEN 'Apartment'
                    ELSE 'House'
                END
            UNION ALL SELECT 'distribution.sequence_windows_with_all_roots', count(*) FROM
                (SELECT ((('x' || right(listing."Id"::text, 12))::bit(48)::bigint - 1) / 1000) AS window_id
                 FROM "Listings" AS listing GROUP BY window_id HAVING count(DISTINCT listing."PropertyType") = 4) AS windows
            UNION ALL
            SELECT 'root.' || lower(listing."PropertyType") || '.status.' || lower(listing."Status"), count(*)
            FROM "Listings" AS listing GROUP BY listing."PropertyType", listing."Status"
            UNION ALL
            SELECT 'root.' || lower(listing."PropertyType") || '.ownership.' ||
                   CASE WHEN listing."AgencyId" IS NULL THEN 'personal' ELSE 'agency' END, count(*)
            FROM "Listings" AS listing GROUP BY listing."PropertyType", listing."AgencyId" IS NULL
            UNION ALL
            SELECT 'root.' || lower(listing."PropertyType") || '.listing_type.' || lower(listing."ListingType"), count(*)
            FROM "Listings" AS listing GROUP BY listing."PropertyType", listing."ListingType"
            UNION ALL
            SELECT 'commercial.' || lower(commercial."CommercialType") || '.status.' || lower(listing."Status"), count(*)
            FROM "Listings" AS listing JOIN "ListingCommercialDetails" AS commercial ON commercial."ListingId" = listing."Id"
            GROUP BY commercial."CommercialType", listing."Status"
            UNION ALL
            SELECT 'commercial.' || lower(commercial."CommercialType") || '.ownership.' ||
                   CASE WHEN listing."AgencyId" IS NULL THEN 'personal' ELSE 'agency' END, count(*)
            FROM "Listings" AS listing JOIN "ListingCommercialDetails" AS commercial ON commercial."ListingId" = listing."Id"
            GROUP BY commercial."CommercialType", listing."AgencyId" IS NULL
            UNION ALL
            SELECT 'commercial.' || lower(commercial."CommercialType") || '.listing_type.' || lower(listing."ListingType"), count(*)
            FROM "Listings" AS listing JOIN "ListingCommercialDetails" AS commercial ON commercial."ListingId" = listing."Id"
            GROUP BY commercial."CommercialType", listing."ListingType"
            UNION ALL
            SELECT 'land.' || lower(land."LandType") || '.status.' || lower(listing."Status"), count(*)
            FROM "Listings" AS listing JOIN "ListingLandDetails" AS land ON land."ListingId" = listing."Id"
            GROUP BY land."LandType", listing."Status"
            UNION ALL
            SELECT 'land.' || lower(land."LandType") || '.ownership.' ||
                   CASE WHEN listing."AgencyId" IS NULL THEN 'personal' ELSE 'agency' END, count(*)
            FROM "Listings" AS listing JOIN "ListingLandDetails" AS land ON land."ListingId" = listing."Id"
            GROUP BY land."LandType", listing."AgencyId" IS NULL
            UNION ALL
            SELECT 'land.' || lower(land."LandType") || '.listing_type.' || lower(listing."ListingType"), count(*)
            FROM "Listings" AS listing JOIN "ListingLandDetails" AS land ON land."ListingId" = listing."Id"
            GROUP BY land."LandType", listing."ListingType"
        )
        SELECT "Name", "Actual"::bigint FROM metrics ORDER BY "Name" COLLATE "C";
        """;

    private const string ProfileIdentitySql = """
        SELECT kind, key, payload
        FROM
        (
            SELECT '01-users' AS kind, "Id"::text AS key, to_jsonb(value)::text AS payload FROM "Users" AS value
            UNION ALL SELECT '02-agencies', "Id"::text, to_jsonb(value)::text FROM "Agencies" AS value
            UNION ALL SELECT '03-members', "Id"::text, to_jsonb(value)::text FROM "AgencyMembers" AS value
            UNION ALL SELECT '04-listings', "Id"::text, to_jsonb(value)::text FROM "Listings" AS value
            UNION ALL SELECT '05-translations', "Id"::text, to_jsonb(value)::text FROM "ListingTranslations" AS value
            UNION ALL SELECT '06-apartment', "ListingId"::text, to_jsonb(value)::text FROM "ListingApartmentDetails" AS value
            UNION ALL SELECT '07-house', "ListingId"::text, to_jsonb(value)::text FROM "ListingHouseDetails" AS value
            UNION ALL SELECT '08-commercial', "ListingId"::text, to_jsonb(value)::text FROM "ListingCommercialDetails" AS value
            UNION ALL SELECT '09-land', "ListingId"::text, to_jsonb(value)::text FROM "ListingLandDetails" AS value
            UNION ALL SELECT '10-images', "Id"::text, to_jsonb(value)::text FROM "ListingImages" AS value
        ) AS profile_rows
        ORDER BY kind COLLATE "C", key COLLATE "C";
        """;
}
