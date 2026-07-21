-- P1-01-filtered-count
-- @filters_Currency: CLR=System.String, DbType=String, NpgsqlDbType=Varchar, Nullable=True, Value=EUR
SELECT count(*)::int
FROM "Listings" AS l
WHERE l."Status" = 'Active' AND l."Currency" = @filters_Currency
