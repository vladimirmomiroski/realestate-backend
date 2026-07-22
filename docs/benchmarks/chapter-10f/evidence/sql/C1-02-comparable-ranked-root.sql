-- C1-02-comparable-ranked-root
-- @municipalityPattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=ComparableMunicipality10F
-- @neighborhoodPattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=ComparableNeighborhood10F
-- @source_AreaSquareMeters: CLR=System.Decimal, DbType=Decimal, NpgsqlDbType=Numeric, Nullable=False, Value=100.00
-- @sourcePricePerSquareMeter: CLR=System.Decimal, DbType=Decimal, NpgsqlDbType=Numeric, Nullable=False, Value=2000
-- @source_Price: CLR=System.Decimal, DbType=Decimal, NpgsqlDbType=Numeric, Nullable=False, Value=200000.00
-- @requestedLanguagePattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=en
-- @macedonianLanguagePattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=mk
-- @source_Id: CLR=System.Guid, DbType=Guid, NpgsqlDbType=Uuid, Nullable=False, Value=40000000-0000-0000-0000-000000000bb9
-- @source_ListingType: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=False, Value=Rent
-- @source_PropertyType: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=False, Value=Apartment
-- @source_Currency: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=EUR
-- @source_LanguageCode: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=en
-- @cityPattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=ComparableCity10F
-- @p: CLR=System.Int32, DbType=Int32, NpgsqlDbType=Integer, Nullable=False, Value=6
SELECT s0."Id", s0."AgencyId", s0."AreaSquareMeters", s0."BalconyCount", s0."Bathrooms", s0."Condition", s0."CreatedAtUtc", s0."CreatedByUserId", s0."Currency", s0."FurnishingStatus", s0."HasBasement", s0."HeatingType", s0."IsExchangePossible", s0."Latitude", s0."ListingType", s0."Longitude", s0."ModifiedAtUtc", s0."Orientation", s0."ParkingSpaces", s0."Price", s0."PropertyType", s0."Rooms", s0."Status", s0."YearBuilt", s0."YearRenovated", l5."ListingId", l5."ApartmentType", l5."Floor", l5."HasElevator", l5."TotalFloors", l6."ListingId", l6."HouseType", l6."NumberOfFloors", l6."YardAreaSquareMeters"
FROM (
    SELECT l."Id", l."AgencyId", l."AreaSquareMeters", l."BalconyCount", l."Bathrooms", l."Condition", l."CreatedAtUtc", l."CreatedByUserId", l."Currency", l."FurnishingStatus", l."HasBasement", l."HeatingType", l."IsExchangePossible", l."Latitude", l."ListingType", l."Longitude", l."ModifiedAtUtc", l."Orientation", l."ParkingSpaces", l."Price", l."PropertyType", l."Rooms", l."Status", l."YearBuilt", l."YearRenovated", CASE
        WHEN s."Municipality" IS NOT NULL AND btrim(s."Municipality", E' \t\n\r') <> '' AND s."Municipality" ILIKE @municipalityPattern ESCAPE '\' AND s."Neighborhood" IS NOT NULL AND btrim(s."Neighborhood", E' \t\n\r') <> '' AND s."Neighborhood" ILIKE @neighborhoodPattern ESCAPE '\' THEN 0
        WHEN s."Municipality" IS NOT NULL AND btrim(s."Municipality", E' \t\n\r') <> '' AND s."Municipality" ILIKE @municipalityPattern ESCAPE '\' THEN 1
        ELSE 2
    END AS c, abs(l."AreaSquareMeters" - @source_AreaSquareMeters) / @source_AreaSquareMeters AS c0, abs(l."Price" / l."AreaSquareMeters" - @sourcePricePerSquareMeter) / @sourcePricePerSquareMeter AS c1, abs(l."Price" - @source_Price) / @source_Price AS c2
    FROM "Listings" AS l
    INNER JOIN (
        SELECT l0."City", l0."LanguageCode", l0."ListingId", l0."Municipality", l0."Neighborhood"
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
                WHERE l3."Status" = 'Active' AND l3."Id" <> @source_Id AND l3."ListingType" = @source_ListingType AND l3."PropertyType" = @source_PropertyType AND l3."Currency" = @source_Currency AND l3."Price" > 0.0 AND l3."AreaSquareMeters" > 0.0
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
            WHERE l1."Status" = 'Active' AND l1."Id" <> @source_Id AND l1."ListingType" = @source_ListingType AND l1."PropertyType" = @source_PropertyType AND l1."Currency" = @source_Currency AND l1."Price" > 0.0 AND l1."AreaSquareMeters" > 0.0
        )
    ) AS s ON l."Id" = s."ListingId"
    WHERE l."Status" = 'Active' AND l."Id" <> @source_Id AND l."ListingType" = @source_ListingType AND l."PropertyType" = @source_PropertyType AND l."Currency" = @source_Currency AND l."Price" > 0.0 AND l."AreaSquareMeters" > 0.0 AND s."LanguageCode" = @source_LanguageCode AND s."City" IS NOT NULL AND btrim(s."City", E' \t\n\r') <> '' AND s."City" ILIKE @cityPattern ESCAPE '\'
    ORDER BY CASE
        WHEN s."Municipality" IS NOT NULL AND btrim(s."Municipality", E' \t\n\r') <> '' AND s."Municipality" ILIKE @municipalityPattern ESCAPE '\' AND s."Neighborhood" IS NOT NULL AND btrim(s."Neighborhood", E' \t\n\r') <> '' AND s."Neighborhood" ILIKE @neighborhoodPattern ESCAPE '\' THEN 0
        WHEN s."Municipality" IS NOT NULL AND btrim(s."Municipality", E' \t\n\r') <> '' AND s."Municipality" ILIKE @municipalityPattern ESCAPE '\' THEN 1
        ELSE 2
    END, abs(l."AreaSquareMeters" - @source_AreaSquareMeters) / @source_AreaSquareMeters, abs(l."Price" / l."AreaSquareMeters" - @sourcePricePerSquareMeter) / @sourcePricePerSquareMeter, abs(l."Price" - @source_Price) / @source_Price, l."CreatedAtUtc" DESC, l."Id" DESC
    LIMIT @p
) AS s0
LEFT JOIN "ListingApartmentDetails" AS l5 ON s0."Id" = l5."ListingId"
LEFT JOIN "ListingHouseDetails" AS l6 ON s0."Id" = l6."ListingId"
ORDER BY s0.c, s0.c0, s0.c1, s0.c2, s0."CreatedAtUtc" DESC, s0."Id" DESC
