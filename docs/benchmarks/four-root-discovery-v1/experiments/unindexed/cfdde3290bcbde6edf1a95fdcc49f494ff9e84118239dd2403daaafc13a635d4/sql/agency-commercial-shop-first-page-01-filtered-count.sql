-- agency-commercial-shop-first-page-01-filtered-count
-- @filters_AgencyId_Value: CLR=System.Guid, DbType=Guid, NpgsqlDbType=Uuid, Nullable=True, Value=20000000-0000-0000-0000-000000000002
-- @filters_CommercialType_Value: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=False, Value=Shop
SELECT count(*)::int
FROM "Listings" AS l
LEFT JOIN "ListingCommercialDetails" AS l0 ON l."Id" = l0."ListingId"
WHERE l."Status" = 'Active' AND l."AgencyId" = @filters_AgencyId_Value AND l."PropertyType" = 'Commercial' AND l0."ListingId" IS NOT NULL AND l0."CommercialType" = @filters_CommercialType_Value
