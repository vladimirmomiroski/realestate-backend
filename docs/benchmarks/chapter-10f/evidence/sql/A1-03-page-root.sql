-- A1-03-page-root
-- @filters_AgencyId_Value: CLR=System.Guid, DbType=Guid, NpgsqlDbType=Uuid, Nullable=True, Value=20000000-0000-0000-0000-000000000001
-- @p2: CLR=System.Int32, DbType=Int32, NpgsqlDbType=Integer, Nullable=False, Value=20
-- @p: CLR=System.Int32, DbType=Int32, NpgsqlDbType=Integer, Nullable=False, Value=0
SELECT l1."Id", l1."AgencyId", l1."AreaSquareMeters", l1."BalconyCount", l1."Bathrooms", l1."Condition", l1."CreatedAtUtc", l1."CreatedByUserId", l1."Currency", l1."FurnishingStatus", l1."HasBasement", l1."HeatingType", l1."IsExchangePossible", l1."Latitude", l1."ListingType", l1."Longitude", l1."ModifiedAtUtc", l1."Orientation", l1."ParkingSpaces", l1."Price", l1."PropertyType", l1."Rooms", l1."Status", l1."YearBuilt", l1."YearRenovated", l0."ListingId", l0."ApartmentType", l0."Floor", l0."HasElevator", l0."TotalFloors", l2."ListingId", l2."HouseType", l2."NumberOfFloors", l2."YardAreaSquareMeters"
FROM (
    SELECT l."Id", l."AgencyId", l."AreaSquareMeters", l."BalconyCount", l."Bathrooms", l."Condition", l."CreatedAtUtc", l."CreatedByUserId", l."Currency", l."FurnishingStatus", l."HasBasement", l."HeatingType", l."IsExchangePossible", l."Latitude", l."ListingType", l."Longitude", l."ModifiedAtUtc", l."Orientation", l."ParkingSpaces", l."Price", l."PropertyType", l."Rooms", l."Status", l."YearBuilt", l."YearRenovated"
    FROM "Listings" AS l
    WHERE l."Status" = 'Active' AND l."AgencyId" = @filters_AgencyId_Value
    ORDER BY l."CreatedAtUtc" DESC, l."Id" DESC
    LIMIT @p2 OFFSET @p
) AS l1
LEFT JOIN "ListingApartmentDetails" AS l0 ON l1."Id" = l0."ListingId"
LEFT JOIN "ListingHouseDetails" AS l2 ON l1."Id" = l2."ListingId"
ORDER BY l1."CreatedAtUtc" DESC, l1."Id" DESC
