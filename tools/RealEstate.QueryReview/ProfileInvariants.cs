using Npgsql;

namespace RealEstate.QueryReview;

internal sealed record ProfileInvariant(string Name, long Expected, long Actual)
{
    public bool IsSatisfied => Expected == Actual;
}

internal sealed class ProfileVerificationResult(IReadOnlyList<ProfileInvariant> invariants)
{
    public IReadOnlyList<ProfileInvariant> Invariants { get; } = invariants;

    public void EnsureValid()
    {
        var failures = Invariants.Where(invariant => !invariant.IsSatisfied).ToArray();

        if (failures.Length == 0)
        {
            return;
        }

        var details = string.Join(
            Environment.NewLine,
            failures.Select(failure =>
                $"  {failure.Name}: expected {failure.Expected:N0}, actual {failure.Actual:N0}"));

        throw new ProfileInvariantException(
            $"The Chapter 10F profile does not satisfy {failures.Length} invariant(s):" +
            Environment.NewLine + details);
    }
}

internal sealed class ProfileInvariantException(string message) : InvalidOperationException(message);

internal static class ProfileInvariants
{
    private static readonly IReadOnlyDictionary<string, long> Expected =
        new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["users.total"] = 101,
            ["users.active"] = 101,
            ["agencies.total"] = 100,
            ["agencies.active"] = 100,
            ["agency_members.total"] = 100,
            ["agency_members.active_owners"] = 100,
            ["listings.total"] = 100_000,
            ["listings.active"] = 70_000,
            ["listings.draft"] = 6_000,
            ["listings.archived"] = 6_000,
            ["listings.reserved"] = 6_000,
            ["listings.sold"] = 6_000,
            ["listings.rented"] = 6_000,
            ["ownership.personal"] = 50_000,
            ["ownership.agency"] = 50_000,
            ["ownership.agency_min"] = 500,
            ["ownership.agency_max"] = 500,
            ["ownership.active_agency_min"] = 350,
            ["ownership.active_agency_max"] = 350,
            ["listing_type.sale"] = 50_000,
            ["listing_type.rent"] = 50_000,
            ["property_type.apartment"] = 50_000,
            ["property_type.house"] = 50_000,
            ["currency.eur"] = 33_334,
            ["currency.usd"] = 33_333,
            ["currency.mkd"] = 33_333,
            ["currency.active_eur"] = 23_334,
            ["currency.active_usd"] = 23_333,
            ["currency.active_mkd"] = 23_333,
            ["rooms.null"] = 20_000,
            ["coordinates.null_pairs"] = 20_000,
            ["coordinates.value_pairs"] = 80_000,
            ["coordinates.partial_pairs"] = 0,
            ["translations.total"] = 200_000,
            ["translations.en"] = 90_000,
            ["translations.mk"] = 100_000,
            ["translations.de"] = 5_000,
            ["translations.sq"] = 5_000,
            ["translations.two_per_listing"] = 100_000,
            ["locations.skopje"] = 28_000,
            ["locations.bitola"] = 14_000,
            ["locations.generic"] = 27_829,
            ["locations.selective"] = 140,
            ["text.broad_title"] = 10_000,
            ["text.selective_q"] = 120,
            ["text.excluded_decoys"] = 500,
            ["comparables.cluster"] = 31,
            ["comparables.eligible_candidates"] = 30,
            ["comparables.tier_0"] = 10,
            ["comparables.tier_1"] = 10,
            ["comparables.tier_2"] = 10,
            ["comparables.area_sum"] = 3_310,
            ["comparables.price_sum"] = 6_599_900,
            ["comparables.equal_timestamp"] = 31,
            ["filters.area_rooms"] = 1_050,
            ["details.apartment"] = 50_000,
            ["details.house"] = 50_000,
            ["details.type_mismatches"] = 0,
            ["images.total"] = 60_000,
            ["images.primary"] = 50_000,
            ["images.secondary"] = 10_000
        };

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
                "The profile invariant query returned an unexpected metric set. " +
                $"Missing: [{string.Join(", ", missing)}]. " +
                $"Unexpected: [{string.Join(", ", unexpected)}].");
        }

        var invariants = Expected
            .Select(expected => new ProfileInvariant(
                expected.Key,
                expected.Value,
                actual[expected.Key]))
            .ToArray();

        return new ProfileVerificationResult(invariants);
    }

    private const string VerificationSql = """
        WITH agency_listing_counts AS
        (
            SELECT a."Id",
                   count(l."Id")::bigint AS "Total",
                   count(l."Id") FILTER (WHERE l."Status" = 'Active')::bigint AS "Active"
            FROM "Agencies" AS a
            LEFT JOIN "Listings" AS l ON l."AgencyId" = a."Id"
            GROUP BY a."Id"
        ),
        translation_counts AS
        (
            SELECT "ListingId", count(*)::bigint AS "Total"
            FROM "ListingTranslations"
            GROUP BY "ListingId"
        ),
        effective_en AS
        (
            SELECT l."Id",
                   l."Status",
                   t."LanguageCode",
                   t."Title",
                   t."Description",
                   t."AddressLine",
                   t."City",
                   t."Municipality",
                   t."Neighborhood"
            FROM "Listings" AS l
            LEFT JOIN LATERAL
            (
                SELECT lt."LanguageCode",
                       lt."Title",
                       lt."Description",
                       lt."AddressLine",
                       lt."City",
                       lt."Municipality",
                       lt."Neighborhood"
                FROM "ListingTranslations" AS lt
                WHERE lt."ListingId" = l."Id"
                ORDER BY CASE
                             WHEN lower(lt."LanguageCode") = 'en' THEN 0
                             WHEN lower(lt."LanguageCode") = 'mk' THEN 1
                             ELSE 2
                         END,
                         lt."LanguageCode" COLLATE "C",
                         lt."Id"
                LIMIT 1
            ) AS t ON TRUE
        ),
        comparable_source AS
        (
            SELECT l."Id",
                   l."ListingType",
                   l."PropertyType",
                   l."Currency",
                   t."LanguageCode",
                   t."City"
            FROM "Listings" AS l
            JOIN LATERAL
            (
                SELECT lt."LanguageCode", lt."City"
                FROM "ListingTranslations" AS lt
                WHERE lt."ListingId" = l."Id"
                ORDER BY CASE
                             WHEN lower(lt."LanguageCode") = 'en' THEN 0
                             WHEN lower(lt."LanguageCode") = 'mk' THEN 1
                             ELSE 2
                         END,
                         lt."LanguageCode" COLLATE "C",
                         lt."Id"
                LIMIT 1
            ) AS t ON TRUE
            WHERE l."Id" = '40000000-0000-0000-0000-000000000bb9'::uuid
              AND l."Status" = 'Active'
        ),
        comparable_candidates AS
        (
            SELECT l."Id"
            FROM "Listings" AS l
            CROSS JOIN comparable_source AS source
            JOIN LATERAL
            (
                SELECT lt."LanguageCode", lt."City"
                FROM "ListingTranslations" AS lt
                WHERE lt."ListingId" = l."Id"
                ORDER BY CASE
                             WHEN lower(lt."LanguageCode") = 'en' THEN 0
                             WHEN lower(lt."LanguageCode") = 'mk' THEN 1
                             ELSE 2
                         END,
                         lt."LanguageCode" COLLATE "C",
                         lt."Id"
                LIMIT 1
            ) AS candidate_translation ON TRUE
            WHERE l."Status" = 'Active'
              AND l."Id" <> source."Id"
              AND l."ListingType" = source."ListingType"
              AND l."PropertyType" = source."PropertyType"
              AND l."Currency" = source."Currency"
              AND l."Price" > 0
              AND l."AreaSquareMeters" > 0
              AND candidate_translation."LanguageCode" = source."LanguageCode"
              AND candidate_translation."City" IS NOT NULL
              AND btrim(candidate_translation."City") <> ''
              AND candidate_translation."City" ILIKE source."City" ESCAPE '\'
        )
        SELECT 'users.total', count(*)::bigint FROM "Users"
        UNION ALL SELECT 'users.active', count(*)::bigint FROM "Users" WHERE "Status" = 'Active'
        UNION ALL SELECT 'agencies.total', count(*)::bigint FROM "Agencies"
        UNION ALL SELECT 'agencies.active', count(*)::bigint FROM "Agencies" WHERE "Status" = 'Active'
        UNION ALL SELECT 'agency_members.total', count(*)::bigint FROM "AgencyMembers"
        UNION ALL SELECT 'agency_members.active_owners', count(*)::bigint FROM "AgencyMembers" WHERE "Status" = 'Active' AND "Role" = 'Owner'
        UNION ALL SELECT 'listings.total', count(*)::bigint FROM "Listings"
        UNION ALL SELECT 'listings.active', count(*)::bigint FROM "Listings" WHERE "Status" = 'Active'
        UNION ALL SELECT 'listings.draft', count(*)::bigint FROM "Listings" WHERE "Status" = 'Draft'
        UNION ALL SELECT 'listings.archived', count(*)::bigint FROM "Listings" WHERE "Status" = 'Archived'
        UNION ALL SELECT 'listings.reserved', count(*)::bigint FROM "Listings" WHERE "Status" = 'Reserved'
        UNION ALL SELECT 'listings.sold', count(*)::bigint FROM "Listings" WHERE "Status" = 'Sold'
        UNION ALL SELECT 'listings.rented', count(*)::bigint FROM "Listings" WHERE "Status" = 'Rented'
        UNION ALL SELECT 'ownership.personal', count(*)::bigint FROM "Listings" WHERE "AgencyId" IS NULL
        UNION ALL SELECT 'ownership.agency', count(*)::bigint FROM "Listings" WHERE "AgencyId" IS NOT NULL
        UNION ALL SELECT 'ownership.agency_min', coalesce(min("Total"), 0)::bigint FROM agency_listing_counts
        UNION ALL SELECT 'ownership.agency_max', coalesce(max("Total"), 0)::bigint FROM agency_listing_counts
        UNION ALL SELECT 'ownership.active_agency_min', coalesce(min("Active"), 0)::bigint FROM agency_listing_counts
        UNION ALL SELECT 'ownership.active_agency_max', coalesce(max("Active"), 0)::bigint FROM agency_listing_counts
        UNION ALL SELECT 'listing_type.sale', count(*)::bigint FROM "Listings" WHERE "ListingType" = 'Sale'
        UNION ALL SELECT 'listing_type.rent', count(*)::bigint FROM "Listings" WHERE "ListingType" = 'Rent'
        UNION ALL SELECT 'property_type.apartment', count(*)::bigint FROM "Listings" WHERE "PropertyType" = 'Apartment'
        UNION ALL SELECT 'property_type.house', count(*)::bigint FROM "Listings" WHERE "PropertyType" = 'House'
        UNION ALL SELECT 'currency.eur', count(*)::bigint FROM "Listings" WHERE "Currency" = 'EUR'
        UNION ALL SELECT 'currency.usd', count(*)::bigint FROM "Listings" WHERE "Currency" = 'USD'
        UNION ALL SELECT 'currency.mkd', count(*)::bigint FROM "Listings" WHERE "Currency" = 'MKD'
        UNION ALL SELECT 'currency.active_eur', count(*)::bigint FROM "Listings" WHERE "Status" = 'Active' AND "Currency" = 'EUR'
        UNION ALL SELECT 'currency.active_usd', count(*)::bigint FROM "Listings" WHERE "Status" = 'Active' AND "Currency" = 'USD'
        UNION ALL SELECT 'currency.active_mkd', count(*)::bigint FROM "Listings" WHERE "Status" = 'Active' AND "Currency" = 'MKD'
        UNION ALL SELECT 'rooms.null', count(*)::bigint FROM "Listings" WHERE "Rooms" IS NULL
        UNION ALL SELECT 'coordinates.null_pairs', count(*)::bigint FROM "Listings" WHERE "Latitude" IS NULL AND "Longitude" IS NULL
        UNION ALL SELECT 'coordinates.value_pairs', count(*)::bigint FROM "Listings" WHERE "Latitude" IS NOT NULL AND "Longitude" IS NOT NULL
        UNION ALL SELECT 'coordinates.partial_pairs', count(*)::bigint FROM "Listings" WHERE ("Latitude" IS NULL) <> ("Longitude" IS NULL)
        UNION ALL SELECT 'translations.total', count(*)::bigint FROM "ListingTranslations"
        UNION ALL SELECT 'translations.en', count(*)::bigint FROM "ListingTranslations" WHERE "LanguageCode" = 'en'
        UNION ALL SELECT 'translations.mk', count(*)::bigint FROM "ListingTranslations" WHERE "LanguageCode" = 'mk'
        UNION ALL SELECT 'translations.de', count(*)::bigint FROM "ListingTranslations" WHERE "LanguageCode" = 'de'
        UNION ALL SELECT 'translations.sq', count(*)::bigint FROM "ListingTranslations" WHERE "LanguageCode" = 'sq'
        UNION ALL SELECT 'translations.two_per_listing', count(*)::bigint FROM translation_counts WHERE "Total" = 2
        UNION ALL SELECT 'locations.skopje', count(*)::bigint FROM effective_en WHERE "Status" = 'Active' AND "City" = 'Skopje'
        UNION ALL SELECT 'locations.bitola', count(*)::bigint FROM effective_en WHERE "Status" = 'Active' AND "City" = 'Bitola'
        UNION ALL SELECT 'locations.generic', count(*)::bigint FROM effective_en WHERE "Status" = 'Active' AND "City" LIKE 'BenchmarkCity%'
        UNION ALL SELECT 'locations.selective', count(*)::bigint FROM effective_en WHERE "Status" = 'Active' AND "City" = 'AuditCity10F' AND "Municipality" = 'AuditMunicipality10F' AND "Neighborhood" = 'AuditNeighborhood10F'
        UNION ALL SELECT 'text.broad_title', count(*)::bigint FROM effective_en WHERE "Status" = 'Active' AND strpos(lower("Title"), 'broadtoken10f') > 0
        UNION ALL SELECT 'text.selective_q', count(*)::bigint FROM effective_en WHERE "Status" = 'Active' AND (strpos(lower("Title"), 'needle10f') > 0 OR strpos(lower(coalesce("City", '')), 'needle10f') > 0 OR strpos(lower(coalesce("Municipality", '')), 'needle10f') > 0 OR strpos(lower(coalesce("Neighborhood", '')), 'needle10f') > 0)
        UNION ALL SELECT 'text.excluded_decoys', count(*)::bigint FROM effective_en WHERE "Status" = 'Active' AND (strpos(lower(coalesce("Description", '')), 'needle10f') > 0 OR strpos(lower(coalesce("AddressLine", '')), 'needle10f') > 0) AND strpos(lower("Title"), 'needle10f') = 0 AND strpos(lower(coalesce("City", '')), 'needle10f') = 0 AND strpos(lower(coalesce("Municipality", '')), 'needle10f') = 0 AND strpos(lower(coalesce("Neighborhood", '')), 'needle10f') = 0
        UNION ALL SELECT 'comparables.cluster', count(*)::bigint FROM effective_en WHERE "Status" = 'Active' AND "LanguageCode" = 'en' AND "City" = 'ComparableCity10F'
        UNION ALL SELECT 'comparables.eligible_candidates', count(*)::bigint FROM comparable_candidates
        UNION ALL SELECT 'comparables.tier_0', count(*)::bigint FROM effective_en WHERE "Id" BETWEEN '40000000-0000-0000-0000-000000000bba'::uuid AND '40000000-0000-0000-0000-000000000bc3'::uuid AND "Municipality" = 'ComparableMunicipality10F' AND "Neighborhood" = 'ComparableNeighborhood10F'
        UNION ALL SELECT 'comparables.tier_1', count(*)::bigint FROM effective_en WHERE "Id" BETWEEN '40000000-0000-0000-0000-000000000bc4'::uuid AND '40000000-0000-0000-0000-000000000bcd'::uuid AND "Municipality" = 'ComparableMunicipality10F' AND "Neighborhood" = 'ComparableNeighborhoodOther10F'
        UNION ALL SELECT 'comparables.tier_2', count(*)::bigint FROM effective_en WHERE "Id" BETWEEN '40000000-0000-0000-0000-000000000bce'::uuid AND '40000000-0000-0000-0000-000000000bd7'::uuid AND "Municipality" = 'ComparableMunicipalityOther10F'
        UNION ALL SELECT 'comparables.area_sum', coalesce(sum("AreaSquareMeters"), 0)::bigint FROM "Listings" WHERE "Id" BETWEEN '40000000-0000-0000-0000-000000000bb9'::uuid AND '40000000-0000-0000-0000-000000000bd7'::uuid
        UNION ALL SELECT 'comparables.price_sum', coalesce(sum("Price"), 0)::bigint FROM "Listings" WHERE "Id" BETWEEN '40000000-0000-0000-0000-000000000bb9'::uuid AND '40000000-0000-0000-0000-000000000bd7'::uuid
        UNION ALL SELECT 'comparables.equal_timestamp', count(*)::bigint FROM "Listings" WHERE "Id" BETWEEN '40000000-0000-0000-0000-000000000bb9'::uuid AND '40000000-0000-0000-0000-000000000bd7'::uuid AND "CreatedAtUtc" = '2026-02-01T00:00:00Z'::timestamptz
        UNION ALL SELECT 'filters.area_rooms', count(*)::bigint FROM "Listings" WHERE "Status" = 'Active' AND "AreaSquareMeters" BETWEEN 80 AND 89 AND "Rooms" BETWEEN 2 AND 3
        UNION ALL SELECT 'details.apartment', count(*)::bigint FROM "ListingApartmentDetails"
        UNION ALL SELECT 'details.house', count(*)::bigint FROM "ListingHouseDetails"
        UNION ALL SELECT 'details.type_mismatches', (SELECT count(*) FROM "ListingApartmentDetails" AS d JOIN "Listings" AS l ON l."Id" = d."ListingId" WHERE l."PropertyType" <> 'Apartment') + (SELECT count(*) FROM "ListingHouseDetails" AS d JOIN "Listings" AS l ON l."Id" = d."ListingId" WHERE l."PropertyType" <> 'House')
        UNION ALL SELECT 'images.total', count(*)::bigint FROM "ListingImages"
        UNION ALL SELECT 'images.primary', count(*)::bigint FROM "ListingImages" WHERE "IsPrimary"
        UNION ALL SELECT 'images.secondary', count(*)::bigint FROM "ListingImages" WHERE NOT "IsPrimary"
        ORDER BY 1;
        """;
}
