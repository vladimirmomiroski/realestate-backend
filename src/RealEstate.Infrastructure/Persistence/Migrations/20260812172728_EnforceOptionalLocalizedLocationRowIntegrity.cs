using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceOptionalLocalizedLocationRowIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_ListingTranslations_AddressLine_TrimmedNonBlank",
                table: "ListingTranslations",
                sql: "\"AddressLine\" IS NULL\nOR (\n    \"AddressLine\" <> ''\n    AND \"AddressLine\" = btrim(\"AddressLine\", chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(32) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288))\n)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ListingTranslations_Municipality_TrimmedNonBlank",
                table: "ListingTranslations",
                sql: "\"Municipality\" IS NULL\nOR (\n    \"Municipality\" <> ''\n    AND \"Municipality\" = btrim(\"Municipality\", chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(32) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288))\n)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ListingTranslations_Neighborhood_TrimmedNonBlank",
                table: "ListingTranslations",
                sql: "\"Neighborhood\" IS NULL\nOR (\n    \"Neighborhood\" <> ''\n    AND \"Neighborhood\" = btrim(\"Neighborhood\", chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(32) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288))\n)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ListingTranslations_AddressLine_TrimmedNonBlank",
                table: "ListingTranslations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ListingTranslations_Municipality_TrimmedNonBlank",
                table: "ListingTranslations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ListingTranslations_Neighborhood_TrimmedNonBlank",
                table: "ListingTranslations");
        }
    }
}
