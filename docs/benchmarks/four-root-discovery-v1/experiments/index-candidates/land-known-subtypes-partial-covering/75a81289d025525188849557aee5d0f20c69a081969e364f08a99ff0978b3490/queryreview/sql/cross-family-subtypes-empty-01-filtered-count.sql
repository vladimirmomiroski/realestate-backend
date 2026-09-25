-- cross-family-subtypes-empty-01-filtered-count
-- @filters_CommercialType_Value: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=False, Value=Office
-- @filters_LandType_Value: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=False, Value=BuildingPlot
SELECT count(*)::int
FROM "Listings" AS l
LEFT JOIN "ListingCommercialDetails" AS l0 ON l."Id" = l0."ListingId"
LEFT JOIN "ListingLandDetails" AS l1 ON l."Id" = l1."ListingId"
WHERE l."Status" = 'Active' AND l."PropertyType" = 'Commercial' AND l0."ListingId" IS NOT NULL AND l0."CommercialType" = @filters_CommercialType_Value AND l."PropertyType" = 'Land' AND l1."ListingId" IS NOT NULL AND l1."LandType" = @filters_LandType_Value
