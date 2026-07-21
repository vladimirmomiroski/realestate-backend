-- R1-01-filtered-count
-- @filters_MinAreaSquareMeters_Value: CLR=System.Decimal, DbType=Decimal, NpgsqlDbType=Numeric, Nullable=False, Value=80
-- @filters_MaxAreaSquareMeters_Value: CLR=System.Decimal, DbType=Decimal, NpgsqlDbType=Numeric, Nullable=False, Value=89
-- @filters_MinRooms_Value: CLR=System.Decimal, DbType=Decimal, NpgsqlDbType=Numeric, Nullable=False, Value=2
-- @filters_MaxRooms_Value: CLR=System.Decimal, DbType=Decimal, NpgsqlDbType=Numeric, Nullable=False, Value=3
SELECT count(*)::int
FROM "Listings" AS l
WHERE l."Status" = 'Active' AND l."AreaSquareMeters" >= @filters_MinAreaSquareMeters_Value AND l."AreaSquareMeters" <= @filters_MaxAreaSquareMeters_Value AND l."Rooms" IS NOT NULL AND l."Rooms" >= @filters_MinRooms_Value AND l."Rooms" <= @filters_MaxRooms_Value
