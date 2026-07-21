-- A1-01-agency-existence
-- @agencyId: CLR=System.Guid, DbType=Guid, NpgsqlDbType=Uuid, Nullable=False, Value=20000000-0000-0000-0000-000000000001
SELECT EXISTS (
    SELECT 1
    FROM "Agencies" AS a
    WHERE a."Id" = @agencyId)
