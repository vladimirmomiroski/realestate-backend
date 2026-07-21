-- P1-03-translation-split
-- @listingIds: CLR=System.Guid[], DbType=Object, NpgsqlDbType=-2147483621, Nullable=True, Value=System.Guid[]
SELECT l0."Id", l0."AddressLine", l0."City", l0."Description", l0."LanguageCode", l0."ListingId", l0."Municipality", l0."Neighborhood", l0."Title"
FROM "Listings" AS l
INNER JOIN "ListingTranslations" AS l0 ON l."Id" = l0."ListingId"
WHERE l."Id" = ANY (@listingIds)
ORDER BY l0."ListingId", l0."Id"
