-- commercial-unknown-first-page-01-filtered-count
-- @filters_CommercialType_Value: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=False, Value=Unknown
SELECT count(*)::int
FROM "Listings" AS l
LEFT JOIN "ListingCommercialDetails" AS l0 ON l."Id" = l0."ListingId"
WHERE l."Status" = 'Active' AND l."PropertyType" = 'Commercial' AND l0."ListingId" IS NOT NULL AND l0."CommercialType" = @filters_CommercialType_Value
