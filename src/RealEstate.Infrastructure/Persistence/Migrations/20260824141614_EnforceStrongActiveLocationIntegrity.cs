using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceStrongActiveLocationIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                LOCK TABLE public."Listings" IN SHARE ROW EXCLUSIVE MODE;
                LOCK TABLE public."ListingTranslations" IN SHARE ROW EXCLUSIVE MODE;

                CREATE OR REPLACE FUNCTION public.re_assert_active_listing_publication_integrity(
                    affected_listing_ids uuid[])
                RETURNS void
                LANGUAGE plpgsql
                SET search_path = pg_catalog
                AS $function$
                DECLARE
                    boundary_whitespace CONSTANT text :=
                        chr(9) || chr(10) || chr(11) || chr(12) || chr(13) ||
                        chr(32) || chr(133) || chr(160) || chr(5760) ||
                        chr(8192) || chr(8193) || chr(8194) || chr(8195) ||
                        chr(8196) || chr(8197) || chr(8198) || chr(8199) ||
                        chr(8200) || chr(8201) || chr(8202) || chr(8232) ||
                        chr(8233) || chr(8239) || chr(8287) || chr(12288);
                BEGIN
                    IF affected_listing_ids IS NULL OR
                       cardinality(affected_listing_ids) = 0 THEN
                        RETURN;
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM public."Listings" AS listing
                        WHERE listing."Id" = ANY(affected_listing_ids)
                          AND listing."Status" = 'Active'
                          AND (
                              NOT EXISTS (
                                  SELECT 1
                                  FROM public."ListingTranslations" AS translation
                                  WHERE translation."ListingId" = listing."Id"
                              )
                              OR EXISTS (
                                  SELECT 1
                                  FROM public."ListingTranslations" AS translation
                                  WHERE translation."ListingId" = listing."Id"
                                    AND (
                                        translation."City" IS NULL
                                        OR translation."Municipality" IS NULL
                                        OR translation."AddressLine" IS NULL
                                        OR translation."Description" IS NULL
                                        OR translation."Municipality" = ''
                                        OR translation."Municipality" <>
                                            btrim(
                                                translation."Municipality",
                                                boundary_whitespace)
                                        OR translation."AddressLine" = ''
                                        OR translation."AddressLine" <>
                                            btrim(
                                                translation."AddressLine",
                                                boundary_whitespace)
                                    )
                              )
                              OR listing."Latitude" IS NULL
                              OR listing."Longitude" IS NULL
                              OR listing."Latitude" NOT BETWEEN -90 AND 90
                              OR listing."Longitude" NOT BETWEEN -180 AND 180
                              OR listing."LocationPrecision" IS NULL
                              OR listing."LocationPrecision" NOT IN (
                                  'ExactAddress',
                                  'Street',
                                  'Neighborhood',
                                  'Municipality',
                                  'City',
                                  'Approximate'
                              )
                              OR listing."GeocodingProviderKey" IS NULL
                              OR listing."GeocodingProviderKey" = ''
                              OR char_length(listing."GeocodingProviderKey") > 64
                              OR listing."GeocodingProviderKey" <>
                                  btrim(
                                      listing."GeocodingProviderKey",
                                      boundary_whitespace)
                              OR listing."GeocodingResultReference" IS NULL
                              OR listing."GeocodingResultReference" = ''
                              OR char_length(listing."GeocodingResultReference") > 512
                              OR listing."GeocodingResultReference" <>
                                  btrim(
                                      listing."GeocodingResultReference",
                                      boundary_whitespace)
                              OR (
                                  listing."GeocodedDisplayName" IS NOT NULL
                                  AND (
                                      listing."GeocodedDisplayName" = ''
                                      OR char_length(listing."GeocodedDisplayName") > 500
                                      OR listing."GeocodedDisplayName" <>
                                          btrim(
                                              listing."GeocodedDisplayName",
                                              boundary_whitespace)
                                  )
                              )
                              OR listing."LocationConfirmedAtUtc" IS NULL
                          )
                    ) THEN
                        RAISE EXCEPTION USING
                            ERRCODE = '23514',
                            MESSAGE = 'Active listing publication integrity violation.';
                    END IF;
                END;
                $function$;

                CREATE OR REPLACE FUNCTION public.re_listings_active_integrity_after_update()
                RETURNS trigger
                LANGUAGE plpgsql
                SET search_path = pg_catalog
                AS $function$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM old_listings AS old_listing
                        INNER JOIN new_listings AS new_listing
                            ON new_listing."Id" = old_listing."Id"
                        WHERE old_listing."Status" = 'Active'
                          AND new_listing."Status" = 'Active'
                          AND (
                              old_listing."Latitude" IS DISTINCT FROM
                                  new_listing."Latitude"
                              OR old_listing."Longitude" IS DISTINCT FROM
                                  new_listing."Longitude"
                              OR old_listing."LocationPrecision" IS DISTINCT FROM
                                  new_listing."LocationPrecision"
                              OR old_listing."GeocodingProviderKey" IS DISTINCT FROM
                                  new_listing."GeocodingProviderKey"
                              OR old_listing."GeocodingResultReference" IS DISTINCT FROM
                                  new_listing."GeocodingResultReference"
                              OR old_listing."GeocodedDisplayName" IS DISTINCT FROM
                                  new_listing."GeocodedDisplayName"
                              OR old_listing."LocationConfirmedAtUtc" IS DISTINCT FROM
                                  new_listing."LocationConfirmedAtUtc"
                          )
                    ) THEN
                        RAISE EXCEPTION USING
                            ERRCODE = '23514',
                            MESSAGE = 'Active listing location is immutable; unpublish before resolving.';
                    END IF;

                    PERFORM public.re_assert_active_listing_publication_integrity(
                        ARRAY(
                            SELECT DISTINCT listing."Id"
                            FROM new_listings AS listing
                            WHERE listing."Status" = 'Active'
                            ORDER BY listing."Id"
                        )
                    );

                    RETURN NULL;
                END;
                $function$;

                DROP TRIGGER "TR_Listings_ActivePublicationIntegrity_Update"
                    ON public."Listings";

                CREATE TRIGGER "TR_Listings_ActivePublicationIntegrity_Update"
                AFTER UPDATE ON public."Listings"
                REFERENCING OLD TABLE AS old_listings NEW TABLE AS new_listings
                FOR EACH STATEMENT
                EXECUTE FUNCTION public.re_listings_active_integrity_after_update();

                SELECT public.re_assert_active_listing_publication_integrity(
                    ARRAY(
                        SELECT listing."Id"
                        FROM public."Listings" AS listing
                        WHERE listing."Status" = 'Active'
                        ORDER BY listing."Id"
                    )
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                LOCK TABLE public."Listings" IN SHARE ROW EXCLUSIVE MODE;
                LOCK TABLE public."ListingTranslations" IN SHARE ROW EXCLUSIVE MODE;

                CREATE OR REPLACE FUNCTION public.re_assert_active_listing_publication_integrity(
                    affected_listing_ids uuid[])
                RETURNS void
                LANGUAGE plpgsql
                SET search_path = pg_catalog
                AS $function$
                BEGIN
                    IF affected_listing_ids IS NULL OR
                       cardinality(affected_listing_ids) = 0 THEN
                        RETURN;
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM public."Listings" AS listing
                        WHERE listing."Id" = ANY(affected_listing_ids)
                          AND listing."Status" = 'Active'
                          AND (
                              NOT EXISTS (
                                  SELECT 1
                                  FROM public."ListingTranslations" AS translation
                                  WHERE translation."ListingId" = listing."Id"
                              )
                              OR EXISTS (
                                  SELECT 1
                                  FROM public."ListingTranslations" AS translation
                                  WHERE translation."ListingId" = listing."Id"
                                    AND (
                                        translation."City" IS NULL
                                        OR translation."Description" IS NULL
                                    )
                              )
                          )
                    ) THEN
                        RAISE EXCEPTION USING
                            ERRCODE = '23514',
                            MESSAGE = 'Active listing publication integrity violation.';
                    END IF;
                END;
                $function$;

                CREATE OR REPLACE FUNCTION public.re_listings_active_integrity_after_update()
                RETURNS trigger
                LANGUAGE plpgsql
                SET search_path = pg_catalog
                AS $function$
                BEGIN
                    PERFORM public.re_assert_active_listing_publication_integrity(
                        ARRAY(
                            SELECT DISTINCT listing."Id"
                            FROM new_listings AS listing
                            WHERE listing."Status" = 'Active'
                            ORDER BY listing."Id"
                        )
                    );

                    RETURN NULL;
                END;
                $function$;

                DROP TRIGGER "TR_Listings_ActivePublicationIntegrity_Update"
                    ON public."Listings";

                CREATE TRIGGER "TR_Listings_ActivePublicationIntegrity_Update"
                AFTER UPDATE ON public."Listings"
                REFERENCING NEW TABLE AS new_listings
                FOR EACH STATEMENT
                EXECUTE FUNCTION public.re_listings_active_integrity_after_update();

                SELECT public.re_assert_active_listing_publication_integrity(
                    ARRAY(
                        SELECT listing."Id"
                        FROM public."Listings" AS listing
                        WHERE listing."Status" = 'Active'
                        ORDER BY listing."Id"
                    )
                );
                """);
        }
    }
}
