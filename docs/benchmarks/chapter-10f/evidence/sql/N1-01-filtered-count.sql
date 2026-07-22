-- N1-01-filtered-count
SELECT count(*)::int
FROM "Listings" AS l
WHERE l."Status" = 'Active'
