-- land-building-plot-root-first-page-02-page-root
-- @filters_PropertyType_Value: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=False, Value=Land
-- @filters_Currency: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=EUR
-- @filters_LandType_Value: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=False, Value=BuildingPlot
-- @p4: CLR=System.Int32, DbType=Int32, NpgsqlDbType=Integer, Nullable=False, Value=20
-- @p: CLR=System.Int32, DbType=Int32, NpgsqlDbType=Integer, Nullable=False, Value=0
SELECT s."Id", s."AgencyId", s."AreaSquareMeters", s."BalconyCount", s."Bathrooms", s."Condition", s."CreatedAtUtc", s."CreatedByUserId", s."Currency", s."FurnishingStatus", s."GeocodedDisplayName", s."GeocodingProviderKey", s."GeocodingResultReference", s."HasBasement", s."HeatingType", s."IsExchangePossible", s."Latitude", s."ListingType", s."LocationConfirmedAtUtc", s."LocationPrecision", s."Longitude", s."ModifiedAtUtc", s."Orientation", s."ParkingSpaces", s."Price", s."PropertyType", s."Rooms", s."Status", s."YearBuilt", s."YearRenovated", l1."ListingId", l1."ApartmentType", l1."Floor", l1."HasElevator", l1."TotalFloors", l2."ListingId", l2."HouseType", l2."NumberOfFloors", l2."YardAreaSquareMeters", l3."ListingId", l3."CommercialType", s."ListingId", s."LandType"
FROM (
    SELECT l."Id", l."AgencyId", l."AreaSquareMeters", l."BalconyCount", l."Bathrooms", l."Condition", l."CreatedAtUtc", l."CreatedByUserId", l."Currency", l."FurnishingStatus", l."GeocodedDisplayName", l."GeocodingProviderKey", l."GeocodingResultReference", l."HasBasement", l."HeatingType", l."IsExchangePossible", l."Latitude", l."ListingType", l."LocationConfirmedAtUtc", l."LocationPrecision", l."Longitude", l."ModifiedAtUtc", l."Orientation", l."ParkingSpaces", l."Price", l."PropertyType", l."Rooms", l."Status", l."YearBuilt", l."YearRenovated", l0."ListingId", l0."LandType"
    FROM "Listings" AS l
    LEFT JOIN "ListingLandDetails" AS l0 ON l."Id" = l0."ListingId"
    WHERE l."Status" = 'Active' AND l."PropertyType" = @filters_PropertyType_Value AND l."Currency" = @filters_Currency AND l."PropertyType" = 'Land' AND l0."ListingId" IS NOT NULL AND l0."LandType" = @filters_LandType_Value
    ORDER BY l."Price" DESC, l."CreatedAtUtc" DESC, l."Id" DESC
    LIMIT @p4 OFFSET @p
) AS s
LEFT JOIN "ListingApartmentDetails" AS l1 ON s."Id" = l1."ListingId"
LEFT JOIN "ListingHouseDetails" AS l2 ON s."Id" = l2."ListingId"
LEFT JOIN "ListingCommercialDetails" AS l3 ON s."Id" = l3."ListingId"
ORDER BY s."Price" DESC, s."CreatedAtUtc" DESC, s."Id" DESC
