-- land-unknown-first-page-01-filtered-count
-- @filters_LandType_Value: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=False, Value=Unknown
SELECT count(*)::int
FROM "Listings" AS l
LEFT JOIN "ListingLandDetails" AS l0 ON l."Id" = l0."ListingId"
WHERE l."Status" = 'Active' AND l."PropertyType" = 'Land' AND l0."ListingId" IS NOT NULL AND l0."LandType" = @filters_LandType_Value
