-- C1-01-comparable-source
-- @requestedLanguagePattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=en
-- @macedonianLanguagePattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=mk
-- @sourceListingId: CLR=System.Guid, DbType=Guid, NpgsqlDbType=Uuid, Nullable=False, Value=40000000-0000-0000-0000-000000000bb9
SELECT l."Id", l."ListingType", l."PropertyType", l."Currency", l."Price", l."AreaSquareMeters", l2."LanguageCode", l2."City", l2."Municipality", l2."Neighborhood"
FROM "Listings" AS l
LEFT JOIN (
    SELECT l1."City", l1."LanguageCode", l1."Municipality", l1."Neighborhood", l1."ListingId0"
    FROM (
        SELECT l0."City", l0."LanguageCode", l0."Municipality", l0."Neighborhood", l0."ListingId" AS "ListingId0", ROW_NUMBER() OVER(PARTITION BY l0."ListingId" ORDER BY CASE
            WHEN l0."LanguageCode" ILIKE @requestedLanguagePattern ESCAPE '\' THEN 0
            WHEN l0."LanguageCode" ILIKE @macedonianLanguagePattern ESCAPE '\' THEN 1
            ELSE 2
        END, l0."LanguageCode" COLLATE "C", l0."Id") AS row
        FROM "ListingTranslations" AS l0
    ) AS l1
    WHERE l1.row <= 1
) AS l2 ON l."Id" = l2."ListingId0"
WHERE l."Id" = @sourceListingId AND l."Status" = 'Active'
LIMIT 1
