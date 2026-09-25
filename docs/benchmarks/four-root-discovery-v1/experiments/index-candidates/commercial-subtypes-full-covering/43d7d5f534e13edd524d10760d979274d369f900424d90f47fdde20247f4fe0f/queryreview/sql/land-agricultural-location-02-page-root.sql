-- land-agricultural-location-02-page-root
-- @requestedLanguagePattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=en
-- @macedonianLanguagePattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=mk
-- @filters_LandType_Value: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=False, Value=AgriculturalLand
-- @cityPattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=Skopje
-- @municipalityPattern: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=Centar
-- @p18: CLR=System.Int32, DbType=Int32, NpgsqlDbType=Integer, Nullable=False, Value=20
-- @p: CLR=System.Int32, DbType=Int32, NpgsqlDbType=Integer, Nullable=False, Value=0
SELECT s0."Id", s0."AgencyId", s0."AreaSquareMeters", s0."BalconyCount", s0."Bathrooms", s0."Condition", s0."CreatedAtUtc", s0."CreatedByUserId", s0."Currency", s0."FurnishingStatus", s0."GeocodedDisplayName", s0."GeocodingProviderKey", s0."GeocodingResultReference", s0."HasBasement", s0."HeatingType", s0."IsExchangePossible", s0."Latitude", s0."ListingType", s0."LocationConfirmedAtUtc", s0."LocationPrecision", s0."Longitude", s0."ModifiedAtUtc", s0."Orientation", s0."ParkingSpaces", s0."Price", s0."PropertyType", s0."Rooms", s0."Status", s0."YearBuilt", s0."YearRenovated", l8."ListingId", l8."ApartmentType", l8."Floor", l8."HasElevator", l8."TotalFloors", l9."ListingId", l9."HouseType", l9."NumberOfFloors", l9."YardAreaSquareMeters", l10."ListingId", l10."CommercialType", s0."ListingId", s0."LandType"
FROM (
    SELECT l."Id", l."AgencyId", l."AreaSquareMeters", l."BalconyCount", l."Bathrooms", l."Condition", l."CreatedAtUtc", l."CreatedByUserId", l."Currency", l."FurnishingStatus", l."GeocodedDisplayName", l."GeocodingProviderKey", l."GeocodingResultReference", l."HasBasement", l."HeatingType", l."IsExchangePossible", l."Latitude", l."ListingType", l."LocationConfirmedAtUtc", l."LocationPrecision", l."Longitude", l."ModifiedAtUtc", l."Orientation", l."ParkingSpaces", l."Price", l."PropertyType", l."Rooms", l."Status", l."YearBuilt", l."YearRenovated", l0."ListingId", l0."LandType"
    FROM "Listings" AS l
    LEFT JOIN "ListingLandDetails" AS l0 ON l."Id" = l0."ListingId"
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
                LEFT JOIN "ListingLandDetails" AS l6 ON l5."Id" = l6."ListingId"
                WHERE l5."Status" = 'Active' AND l5."PropertyType" = 'Land' AND l6."ListingId" IS NOT NULL AND l6."LandType" = @filters_LandType_Value
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
            LEFT JOIN "ListingLandDetails" AS l3 ON l2."Id" = l3."ListingId"
            WHERE l2."Status" = 'Active' AND l2."PropertyType" = 'Land' AND l3."ListingId" IS NOT NULL AND l3."LandType" = @filters_LandType_Value
        ) AND l1."City" IS NOT NULL AND l1."City" ILIKE @cityPattern ESCAPE '\' AND l1."Municipality" IS NOT NULL AND l1."Municipality" ILIKE @municipalityPattern ESCAPE '\'
    ) AS s ON l."Id" = s."ListingId"
    WHERE l."Status" = 'Active' AND l."PropertyType" = 'Land' AND l0."ListingId" IS NOT NULL AND l0."LandType" = @filters_LandType_Value
    ORDER BY l."CreatedAtUtc" DESC, l."Id" DESC
    LIMIT @p18 OFFSET @p
) AS s0
LEFT JOIN "ListingApartmentDetails" AS l8 ON s0."Id" = l8."ListingId"
LEFT JOIN "ListingHouseDetails" AS l9 ON s0."Id" = l9."ListingId"
LEFT JOIN "ListingCommercialDetails" AS l10 ON s0."Id" = l10."ListingId"
ORDER BY s0."CreatedAtUtc" DESC, s0."Id" DESC
