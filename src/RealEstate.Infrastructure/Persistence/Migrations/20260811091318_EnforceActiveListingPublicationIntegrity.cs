using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceActiveListingPublicationIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                LOCK TABLE public."Listings" IN SHARE ROW EXCLUSIVE MODE;
                LOCK TABLE public."ListingTranslations" IN SHARE ROW EXCLUSIVE MODE;

                CREATE FUNCTION public.re_assert_active_listing_publication_integrity(
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

                CREATE FUNCTION public.re_guard_listing_translation_parent_mutation(
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

                    PERFORM listing."Id"
                    FROM public."Listings" AS listing
                    WHERE listing."Id" = ANY(affected_listing_ids)
                    ORDER BY listing."Id"
                    FOR UPDATE;

                    IF EXISTS (
                        SELECT 1
                        FROM public."Listings" AS listing
                        WHERE listing."Id" = ANY(affected_listing_ids)
                          AND listing."Status" = 'Active'
                    ) THEN
                        RAISE EXCEPTION USING
                            ERRCODE = '23514',
                            MESSAGE = 'Active listing translations are immutable; unpublish before editing.';
                    END IF;

                    UPDATE public."Listings" AS listing
                    SET "Status" = listing."Status"
                    WHERE listing."Id" = ANY(affected_listing_ids)
                      AND listing."Status" = 'Draft';
                END;
                $function$;

                CREATE FUNCTION public.re_listings_active_integrity_after_insert()
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

                CREATE FUNCTION public.re_listings_active_integrity_after_update()
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

                CREATE FUNCTION public.re_listing_translations_guard_after_insert()
                RETURNS trigger
                LANGUAGE plpgsql
                SET search_path = pg_catalog
                AS $function$
                BEGIN
                    PERFORM public.re_guard_listing_translation_parent_mutation(
                        ARRAY(
                            SELECT DISTINCT translation."ListingId"
                            FROM new_translations AS translation
                            ORDER BY translation."ListingId"
                        )
                    );

                    RETURN NULL;
                END;
                $function$;

                CREATE FUNCTION public.re_listing_translations_guard_after_update()
                RETURNS trigger
                LANGUAGE plpgsql
                SET search_path = pg_catalog
                AS $function$
                BEGIN
                    PERFORM public.re_guard_listing_translation_parent_mutation(
                        ARRAY(
                            SELECT affected_parent."ListingId"
                            FROM (
                                SELECT translation."ListingId"
                                FROM old_translations AS translation
                                UNION
                                SELECT translation."ListingId"
                                FROM new_translations AS translation
                            ) AS affected_parent
                            ORDER BY affected_parent."ListingId"
                        )
                    );

                    RETURN NULL;
                END;
                $function$;

                CREATE FUNCTION public.re_listing_translations_guard_after_delete()
                RETURNS trigger
                LANGUAGE plpgsql
                SET search_path = pg_catalog
                AS $function$
                BEGIN
                    PERFORM public.re_guard_listing_translation_parent_mutation(
                        ARRAY(
                            SELECT DISTINCT translation."ListingId"
                            FROM old_translations AS translation
                            ORDER BY translation."ListingId"
                        )
                    );

                    RETURN NULL;
                END;
                $function$;

                CREATE TRIGGER "TR_Listings_ActivePublicationIntegrity_Insert"
                AFTER INSERT ON public."Listings"
                REFERENCING NEW TABLE AS new_listings
                FOR EACH STATEMENT
                EXECUTE FUNCTION public.re_listings_active_integrity_after_insert();

                CREATE TRIGGER "TR_Listings_ActivePublicationIntegrity_Update"
                AFTER UPDATE ON public."Listings"
                REFERENCING NEW TABLE AS new_listings
                FOR EACH STATEMENT
                EXECUTE FUNCTION public.re_listings_active_integrity_after_update();

                CREATE TRIGGER "TR_ListingTranslations_ActiveFreeze_Insert"
                AFTER INSERT ON public."ListingTranslations"
                REFERENCING NEW TABLE AS new_translations
                FOR EACH STATEMENT
                EXECUTE FUNCTION public.re_listing_translations_guard_after_insert();

                CREATE TRIGGER "TR_ListingTranslations_ActiveFreeze_Update"
                AFTER UPDATE ON public."ListingTranslations"
                REFERENCING OLD TABLE AS old_translations NEW TABLE AS new_translations
                FOR EACH STATEMENT
                EXECUTE FUNCTION public.re_listing_translations_guard_after_update();

                CREATE TRIGGER "TR_ListingTranslations_ActiveFreeze_Delete"
                AFTER DELETE ON public."ListingTranslations"
                REFERENCING OLD TABLE AS old_translations
                FOR EACH STATEMENT
                EXECUTE FUNCTION public.re_listing_translations_guard_after_delete();

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
                DROP TRIGGER IF EXISTS "TR_ListingTranslations_ActiveFreeze_Delete"
                    ON public."ListingTranslations";
                DROP TRIGGER IF EXISTS "TR_ListingTranslations_ActiveFreeze_Update"
                    ON public."ListingTranslations";
                DROP TRIGGER IF EXISTS "TR_ListingTranslations_ActiveFreeze_Insert"
                    ON public."ListingTranslations";
                DROP TRIGGER IF EXISTS "TR_Listings_ActivePublicationIntegrity_Update"
                    ON public."Listings";
                DROP TRIGGER IF EXISTS "TR_Listings_ActivePublicationIntegrity_Insert"
                    ON public."Listings";

                DROP FUNCTION IF EXISTS public.re_listing_translations_guard_after_delete();
                DROP FUNCTION IF EXISTS public.re_listing_translations_guard_after_update();
                DROP FUNCTION IF EXISTS public.re_listing_translations_guard_after_insert();
                DROP FUNCTION IF EXISTS public.re_listings_active_integrity_after_update();
                DROP FUNCTION IF EXISTS public.re_listings_active_integrity_after_insert();
                DROP FUNCTION IF EXISTS public.re_guard_listing_translation_parent_mutation(uuid[]);
                DROP FUNCTION IF EXISTS public.re_assert_active_listing_publication_integrity(uuid[]);
                """);
        }
    }
}
