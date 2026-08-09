using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceListingTranslationRowIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_ListingTranslations_City_TrimmedNonBlank",
                table: "ListingTranslations",
                sql: "\"City\" IS NULL\nOR (\n    \"City\" <> ''\n    AND \"City\" = btrim(\"City\", chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(32) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288))\n)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ListingTranslations_Description_TrimmedNonBlank",
                table: "ListingTranslations",
                sql: "\"Description\" IS NULL\nOR (\n    \"Description\" <> ''\n    AND \"Description\" = btrim(\"Description\", chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(32) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288))\n)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ListingTranslations_LanguageCode_Canonical",
                table: "ListingTranslations",
                sql: "\"LanguageCode\" <> ''\nAND \"LanguageCode\" = btrim(\"LanguageCode\", chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(32) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288))\nAND \"LanguageCode\" = lower(\"LanguageCode\")\nAND \"LanguageCode\" ~ '^[a-z]{2,3}(-[a-z0-9]{2,8})*$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ListingTranslations_Title_TrimmedNonBlank",
                table: "ListingTranslations",
                sql: "\"Title\" <> ''\nAND \"Title\" = btrim(\"Title\", chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(32) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ListingTranslations_City_TrimmedNonBlank",
                table: "ListingTranslations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ListingTranslations_Description_TrimmedNonBlank",
                table: "ListingTranslations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ListingTranslations_LanguageCode_Canonical",
                table: "ListingTranslations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ListingTranslations_Title_TrimmedNonBlank",
                table: "ListingTranslations");
        }
    }
}
