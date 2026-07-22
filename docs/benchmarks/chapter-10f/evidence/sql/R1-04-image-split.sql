-- R1-04-image-split
-- @listingIds: CLR=System.Guid[], DbType=Object, NpgsqlDbType=-2147483621, Nullable=True, Value=System.Guid[]
SELECT l0."Id", l0."ContentType", l0."CreatedAtUtc", l0."IsPrimary", l0."ListingId", l0."ModifiedAtUtc", l0."OriginalFileName", l0."SizeBytes", l0."SortOrder", l0."StoredFileName", l0."Url"
FROM "Listings" AS l
INNER JOIN "ListingImages" AS l0 ON l."Id" = l0."ListingId"
WHERE l."Id" = ANY (@listingIds)
ORDER BY l0."ListingId", l0."SortOrder", l0."Id"
