-- L1-02-page-root
-- @requestedLanguagePattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=en
-- @macedonianLanguagePattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=mk
-- @cityPattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=AuditCity10F
-- @municipalityPattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=AuditMunicipality10F
-- @neighborhoodPattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=AuditNeighborhood10F
-- @p17: CLR=System.Int32, DbType=Int32, NpgsqlDbType=Integer, Nullable=False, Value=20
-- @p: CLR=System.Int32, DbType=Int32, NpgsqlDbType=Integer, Nullable=False, Value=0
SELECT s0."Id", s0."AgencyId", s0."AreaSquareMeters", s0."BalconyCount", s0."Bathrooms", s0."Condition", s0."CreatedAtUtc", s0."CreatedByUserId", s0."Currency", s0."FurnishingStatus", s0."HasBasement", s0."HeatingType", s0."IsExchangePossible", s0."Latitude", s0."ListingType", s0."Longitude", s0."ModifiedAtUtc", s0."Orientation", s0."ParkingSpaces", s0."Price", s0."PropertyType", s0."Rooms", s0."Status", s0."YearBuilt", s0."YearRenovated", l5."ListingId", l5."ApartmentType", l5."Floor", l5."HasElevator", l5."TotalFloors", l6."ListingId", l6."HouseType", l6."NumberOfFloors", l6."YardAreaSquareMeters"
FROM (
    SELECT l."Id", l."AgencyId", l."AreaSquareMeters", l."BalconyCount", l."Bathrooms", l."Condition", l."CreatedAtUtc", l."CreatedByUserId", l."Currency", l."FurnishingStatus", l."HasBasement", l."HeatingType", l."IsExchangePossible", l."Latitude", l."ListingType", l."Longitude", l."ModifiedAtUtc", l."Orientation", l."ParkingSpaces", l."Price", l."PropertyType", l."Rooms", l."Status", l."YearBuilt", l."YearRenovated"
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
    ORDER BY l."CreatedAtUtc" DESC, l."Id" DESC
    LIMIT @p17 OFFSET @p
) AS s0
LEFT JOIN "ListingApartmentDetails" AS l5 ON s0."Id" = l5."ListingId"
LEFT JOIN "ListingHouseDetails" AS l6 ON s0."Id" = l6."ListingId"
ORDER BY s0."CreatedAtUtc" DESC, s0."Id" DESC
