-- A1-02-filtered-count
-- @filters_AgencyId_Value: CLR=System.Guid, DbType=Guid, NpgsqlDbType=Uuid, Nullable=True, Value=20000000-0000-0000-0000-000000000001
SELECT count(*)::int
FROM "Listings" AS l
WHERE l."Status" = 'Active' AND l."AgencyId" = @filters_AgencyId_Value
