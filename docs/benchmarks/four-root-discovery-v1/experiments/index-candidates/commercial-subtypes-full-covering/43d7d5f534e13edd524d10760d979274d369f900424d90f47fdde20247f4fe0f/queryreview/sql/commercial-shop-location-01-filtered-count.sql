-- commercial-shop-location-01-filtered-count
-- @requestedLanguagePattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=en
-- @macedonianLanguagePattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=mk
-- @filters_CommercialType_Value: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=False, Value=Shop
-- @cityPattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=Skopje
-- @municipalityPattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=Centar
SELECT count(*)::int
FROM "Listings" AS l
LEFT JOIN "ListingCommercialDetails" AS l0 ON l."Id" = l0."ListingId"
INNER JOIN (
    SELECT l1."ListingId"
    FROM "ListingTranslations" AS l1
    INNER JOIN (
        SELECT l4."ListingId", min(CASE
            WHEN l4."LanguageCode" ILIKE @requestedLanguagePattern ESCAPE '\' THEN '0'
            WHEN l4."LanguageCode" ILIKE @macedonianLanguagePattern ESCAPE '\' THEN '1'
            ELSE '2'
        END || l4."LanguageCode" COLLATE "C") AS "LanguageSelectionKey"
        FROM "ListingTranslations" AS l4
        WHERE l4."ListingId" IN (
            SELECT l5."Id"
            FROM "Listings" AS l5
            LEFT JOIN "ListingCommercialDetails" AS l6 ON l5."Id" = l6."ListingId"
            WHERE l5."Status" = 'Active' AND l5."PropertyType" = 'Commercial' AND l6."ListingId" IS NOT NULL AND l6."CommercialType" = @filters_CommercialType_Value
        )
        GROUP BY l4."ListingId"
    ) AS l7 ON l1."ListingId" = l7."ListingId" AND CASE
        WHEN l1."LanguageCode" ILIKE @requestedLanguagePattern ESCAPE '\' THEN '0'
        WHEN l1."LanguageCode" ILIKE @macedonianLanguagePattern ESCAPE '\' THEN '1'
        ELSE '2'
    END || l1."LanguageCode" COLLATE "C" = l7."LanguageSelectionKey"
    WHERE l1."ListingId" IN (
        SELECT l2."Id"
        FROM "Listings" AS l2
        LEFT JOIN "ListingCommercialDetails" AS l3 ON l2."Id" = l3."ListingId"
        WHERE l2."Status" = 'Active' AND l2."PropertyType" = 'Commercial' AND l3."ListingId" IS NOT NULL AND l3."CommercialType" = @filters_CommercialType_Value
    ) AND l1."City" IS NOT NULL AND l1."City" ILIKE @cityPattern ESCAPE '\' AND l1."Municipality" IS NOT NULL AND l1."Municipality" ILIKE @municipalityPattern ESCAPE '\'
) AS s ON l."Id" = s."ListingId"
WHERE l."Status" = 'Active' AND l."PropertyType" = 'Commercial' AND l0."ListingId" IS NOT NULL AND l0."CommercialType" = @filters_CommercialType_Value
