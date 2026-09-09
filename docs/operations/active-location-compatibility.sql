-- Chapter 13J.2 controlled-operator compatibility report.
-- Prerequisite: the target database is migrated through 13H.3/13I.12.
-- Output is intentionally limited to an incompatible count, listing IDs,
-- and stable incompatibility-family codes.
WITH settings AS MATERIALIZED
(
    SELECT
        chr(9) || chr(10) || chr(11) || chr(12) || chr(13) ||
        chr(32) || chr(133) || chr(160) || chr(5760) ||
        chr(8192) || chr(8193) || chr(8194) || chr(8195) ||
        chr(8196) || chr(8197) || chr(8198) || chr(8199) ||
        chr(8200) || chr(8201) || chr(8202) || chr(8232) ||
        chr(8233) || chr(8239) || chr(8287) || chr(12288)
            AS boundary_whitespace
),
active_listings AS MATERIALIZED
(
    SELECT
        listing."Id",
        listing."Latitude",
        listing."Longitude",
        listing."LocationPrecision",
        listing."GeocodingProviderKey",
        listing."GeocodingResultReference",
        listing."GeocodedDisplayName",
        listing."LocationConfirmedAtUtc"
    FROM "Listings" AS listing
    WHERE listing."Status" = 'Active'
),
translation_flags AS MATERIALIZED
(
    SELECT
        translation."ListingId",
        bool_or(
            translation."Municipality" IS NULL OR
            btrim(
                translation."Municipality",
                settings.boundary_whitespace) = '')
            AS missing_or_blank_municipality,
        bool_or(
            translation."AddressLine" IS NULL OR
            btrim(
                translation."AddressLine",
                settings.boundary_whitespace) = '')
            AS missing_or_blank_address_line
    FROM "ListingTranslations" AS translation
    INNER JOIN active_listings AS listing
        ON listing."Id" = translation."ListingId"
    CROSS JOIN settings
    GROUP BY translation."ListingId"
),
classified AS
(
    SELECT
        listing."Id" AS listing_id,
        array_remove(
            ARRAY
            [
                CASE
                    WHEN COALESCE(
                        flags.missing_or_blank_municipality,
                        false)
                    THEN 'translation_missing_or_blank_municipality'
                END,
                CASE
                    WHEN COALESCE(
                        flags.missing_or_blank_address_line,
                        false)
                    THEN 'translation_missing_or_blank_address_line'
                END,
                root_state.family
            ],
            NULL) AS families
    FROM active_listings AS listing
    LEFT JOIN translation_flags AS flags
        ON flags."ListingId" = listing."Id"
    CROSS JOIN settings
    CROSS JOIN LATERAL
    (
        SELECT CASE
            WHEN
                listing."Latitude" IS NULL AND
                listing."Longitude" IS NULL AND
                listing."LocationPrecision" IS NULL AND
                listing."GeocodingProviderKey" IS NULL AND
                listing."GeocodingResultReference" IS NULL AND
                listing."GeocodedDisplayName" IS NULL AND
                listing."LocationConfirmedAtUtc" IS NULL
            THEN 'root_unresolved'
            WHEN
                listing."Latitude" IS NOT NULL AND
                listing."Longitude" IS NOT NULL AND
                listing."LocationPrecision" IS NULL AND
                listing."GeocodingProviderKey" IS NULL AND
                listing."GeocodingResultReference" IS NULL AND
                listing."GeocodedDisplayName" IS NULL AND
                listing."LocationConfirmedAtUtc" IS NULL
            THEN 'root_legacy_unverified'
            WHEN
                listing."Latitude" IS NOT NULL AND
                listing."Longitude" IS NOT NULL AND
                listing."LocationPrecision" IS NOT NULL AND
                listing."GeocodingProviderKey" IS NOT NULL AND
                listing."GeocodingResultReference" IS NOT NULL AND
                listing."LocationConfirmedAtUtc" IS NOT NULL
            THEN CASE
                WHEN
                    listing."Latitude" BETWEEN -90 AND 90 AND
                    listing."Longitude" BETWEEN -180 AND 180 AND
                    listing."LocationPrecision" IN
                    (
                        'ExactAddress',
                        'Street',
                        'Neighborhood',
                        'Municipality',
                        'City',
                        'Approximate'
                    ) AND
                    char_length(listing."GeocodingProviderKey") <= 64 AND
                    listing."GeocodingProviderKey" <> '' AND
                    listing."GeocodingProviderKey" = btrim(
                        listing."GeocodingProviderKey",
                        settings.boundary_whitespace) AND
                    char_length(listing."GeocodingResultReference") <= 512 AND
                    listing."GeocodingResultReference" <> '' AND
                    listing."GeocodingResultReference" = btrim(
                        listing."GeocodingResultReference",
                        settings.boundary_whitespace) AND
                    (
                        listing."GeocodedDisplayName" IS NULL OR
                        (
                            char_length(listing."GeocodedDisplayName") <= 500 AND
                            listing."GeocodedDisplayName" <> '' AND
                            listing."GeocodedDisplayName" = btrim(
                                listing."GeocodedDisplayName",
                                settings.boundary_whitespace)
                        )
                    )
                THEN NULL
                ELSE 'root_invalid_confirmed'
            END
            ELSE 'root_partial'
        END AS family
    ) AS root_state
),
incompatible AS
(
    SELECT classified.listing_id, classified.families
    FROM classified
    WHERE cardinality(classified.families) > 0
)
SELECT
    count(*)::bigint AS "IncompatibleCount",
    COALESCE(
        jsonb_agg(
            jsonb_build_object(
                'listingId', incompatible.listing_id,
                'families', incompatible.families)
            ORDER BY incompatible.listing_id),
        '[]'::jsonb) AS "IncompatibleListings"
FROM incompatible;
