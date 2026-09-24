-- commercial-office-root-first-page-01-filtered-count
-- @filters_PropertyType_Value: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=False, Value=Commercial
-- @filters_Currency: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=EUR
-- @filters_CommercialType_Value: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=False, Value=Office
SELECT count(*)::int
FROM "Listings" AS l
LEFT JOIN "ListingCommercialDetails" AS l0 ON l."Id" = l0."ListingId"
WHERE l."Status" = 'Active' AND l."PropertyType" = @filters_PropertyType_Value AND l."Currency" = @filters_Currency AND l."PropertyType" = 'Commercial' AND l0."ListingId" IS NOT NULL AND l0."CommercialType" = @filters_CommercialType_Value
