-- L1-01-filtered-count
-- @requestedLanguagePattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=en
-- @macedonianLanguagePattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=mk
-- @cityPattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=AuditCity10F
-- @municipalityPattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=AuditMunicipality10F
-- @neighborhoodPattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=AuditNeighborhood10F
SELECT count(*)::int
FROM "Listings" AS l
INNER JOIN (
    SELECT l0."ListingId"
    FROM "ListingTranslations" AS l0
    INNER JOIN (
        SELECT l2."ListingId", min(CASE
            WHEN l2."LanguageCode" ILIKE @requestedLanguagePattern ESCAPE '\' THEN '0'
            WHEN l2."LanguageCode" ILIKE @macedonianLanguagePattern ESCAPE '\' THEN '1'
            ELSE '2'
        END || l2."LanguageCode" COLLATE "C") AS "LanguageSelectionKey"
        FROM "ListingTranslations" AS l2
        WHERE l2."ListingId" IN (
            SELECT l3."Id"
            FROM "Listings" AS l3
            WHERE l3."Status" = 'Active'
        )
        GROUP BY l2."ListingId"
    ) AS l4 ON l0."ListingId" = l4."ListingId" AND CASE
        WHEN l0."LanguageCode" ILIKE @requestedLanguagePattern ESCAPE '\' THEN '0'
        WHEN l0."LanguageCode" ILIKE @macedonianLanguagePattern ESCAPE '\' THEN '1'
        ELSE '2'
    END || l0."LanguageCode" COLLATE "C" = l4."LanguageSelectionKey"
    WHERE l0."ListingId" IN (
        SELECT l1."Id"
        FROM "Listings" AS l1
        WHERE l1."Status" = 'Active'
    ) AND l0."City" IS NOT NULL AND l0."City" ILIKE @cityPattern ESCAPE '\' AND l0."Municipality" IS NOT NULL AND l0."Municipality" ILIKE @municipalityPattern ESCAPE '\' AND l0."Neighborhood" IS NOT NULL AND l0."Neighborhood" ILIKE @neighborhoodPattern ESCAPE '\'
) AS s ON l."Id" = s."ListingId"
WHERE l."Status" = 'Active'
