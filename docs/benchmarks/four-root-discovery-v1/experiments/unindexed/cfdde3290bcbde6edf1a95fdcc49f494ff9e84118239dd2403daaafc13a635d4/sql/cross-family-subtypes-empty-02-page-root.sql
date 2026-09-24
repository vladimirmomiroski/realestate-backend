-- cross-family-subtypes-empty-02-page-root
-- @filters_CommercialType_Value: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=False, Value=Office
-- @filters_LandType_Value: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=False, Value=BuildingPlot
-- @p3: CLR=System.Int32, DbType=Int32, NpgsqlDbType=Integer, Nullable=False, Value=20
-- @p: CLR=System.Int32, DbType=Int32, NpgsqlDbType=Integer, Nullable=False, Value=0
SELECT s."Id", s."AgencyId", s."AreaSquareMeters", s."BalconyCount", s."Bathrooms", s."Condition", s."CreatedAtUtc", s."CreatedByUserId", s."Currency", s."FurnishingStatus", s."GeocodedDisplayName", s."GeocodingProviderKey", s."GeocodingResultReference", s."HasBasement", s."HeatingType", s."IsExchangePossible", s."Latitude", s."ListingType", s."LocationConfirmedAtUtc", s."LocationPrecision", s."Longitude", s."ModifiedAtUtc", s."Orientation", s."ParkingSpaces", s."Price", s."PropertyType", s."Rooms", s."Status", s."YearBuilt", s."YearRenovated", l2."ListingId", l2."ApartmentType", l2."Floor", l2."HasElevator", l2."TotalFloors", l3."ListingId", l3."HouseType", l3."NumberOfFloors", l3."YardAreaSquareMeters", s."ListingId", s."CommercialType", s."ListingId0", s."LandType"
FROM (
    SELECT l."Id", l."AgencyId", l."AreaSquareMeters", l."BalconyCount", l."Bathrooms", l."Condition", l."CreatedAtUtc", l."CreatedByUserId", l."Currency", l."FurnishingStatus", l."GeocodedDisplayName", l."GeocodingProviderKey", l."GeocodingResultReference", l."HasBasement", l."HeatingType", l."IsExchangePossible", l."Latitude", l."ListingType", l."LocationConfirmedAtUtc", l."LocationPrecision", l."Longitude", l."ModifiedAtUtc", l."Orientation", l."ParkingSpaces", l."Price", l."PropertyType", l."Rooms", l."Status", l."YearBuilt", l."YearRenovated", l0."ListingId", l0."CommercialType", l1."ListingId" AS "ListingId0", l1."LandType"
    FROM "Listings" AS l
    LEFT JOIN "ListingCommercialDetails" AS l0 ON l."Id" = l0."ListingId"
    LEFT JOIN "ListingLandDetails" AS l1 ON l."Id" = l1."ListingId"
    WHERE l."Status" = 'Active' AND l."PropertyType" = 'Commercial' AND l0."ListingId" IS NOT NULL AND l0."CommercialType" = @filters_CommercialType_Value AND l."PropertyType" = 'Land' AND l1."ListingId" IS NOT NULL AND l1."LandType" = @filters_LandType_Value
    ORDER BY l."CreatedAtUtc" DESC, l."Id" DESC
    LIMIT @p3 OFFSET @p
) AS s
LEFT JOIN "ListingApartmentDetails" AS l2 ON s."Id" = l2."ListingId"
LEFT JOIN "ListingHouseDetails" AS l3 ON s."Id" = l3."ListingId"
ORDER BY s."CreatedAtUtc" DESC, s."Id" DESC
