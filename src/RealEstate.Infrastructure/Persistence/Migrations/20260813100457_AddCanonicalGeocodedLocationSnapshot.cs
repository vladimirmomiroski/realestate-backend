using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalGeocodedLocationSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GeocodedDisplayName",
                table: "Listings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeocodingProviderKey",
                table: "Listings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeocodingResultReference",
                table: "Listings",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LocationConfirmedAtUtc",
                table: "Listings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationPrecision",
                table: "Listings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Listings_Location_CoordinatePair",
                table: "Listings",
                sql: "(\n    \"Latitude\" IS NULL\n    AND \"Longitude\" IS NULL\n)\nOR (\n    \"Latitude\" IS NOT NULL\n    AND \"Longitude\" IS NOT NULL\n)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Listings_Location_DisplayNameTrimmedNonBlank",
                table: "Listings",
                sql: "\"GeocodedDisplayName\" IS NULL\nOR (\n    \"GeocodedDisplayName\" <> ''\n    AND \"GeocodedDisplayName\" = btrim(\"GeocodedDisplayName\", chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(32) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288))\n)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Listings_Location_LatitudeRange",
                table: "Listings",
                sql: "\"Latitude\" IS NULL OR \"Latitude\" BETWEEN -90 AND 90");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Listings_Location_LongitudeRange",
                table: "Listings",
                sql: "\"Longitude\" IS NULL OR \"Longitude\" BETWEEN -180 AND 180");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Listings_Location_PrecisionDefined",
                table: "Listings",
                sql: "\"LocationPrecision\" IS NULL\nOR \"LocationPrecision\" IN (\n    'ExactAddress',\n    'Street',\n    'Neighborhood',\n    'Municipality',\n    'City',\n    'Approximate')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Listings_Location_ProviderKeyTrimmedNonBlank",
                table: "Listings",
                sql: "\"GeocodingProviderKey\" IS NULL\nOR (\n    \"GeocodingProviderKey\" <> ''\n    AND \"GeocodingProviderKey\" = btrim(\"GeocodingProviderKey\", chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(32) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288))\n)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Listings_Location_ResultReferenceTrimmedNonBlank",
                table: "Listings",
                sql: "\"GeocodingResultReference\" IS NULL\nOR (\n    \"GeocodingResultReference\" <> ''\n    AND \"GeocodingResultReference\" = btrim(\"GeocodingResultReference\", chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(32) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288))\n)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Listings_Location_SnapshotState",
                table: "Listings",
                sql: "(\n    \"Latitude\" IS NULL\n    AND \"Longitude\" IS NULL\n    AND \"LocationPrecision\" IS NULL\n    AND \"GeocodingProviderKey\" IS NULL\n    AND \"GeocodingResultReference\" IS NULL\n    AND \"GeocodedDisplayName\" IS NULL\n    AND \"LocationConfirmedAtUtc\" IS NULL\n)\nOR (\n    \"Latitude\" IS NOT NULL\n    AND \"Longitude\" IS NOT NULL\n    AND (\n        (\n            \"LocationPrecision\" IS NULL\n            AND \"GeocodingProviderKey\" IS NULL\n            AND \"GeocodingResultReference\" IS NULL\n            AND \"GeocodedDisplayName\" IS NULL\n            AND \"LocationConfirmedAtUtc\" IS NULL\n        )\n        OR (\n            \"LocationPrecision\" IS NOT NULL\n            AND \"GeocodingProviderKey\" IS NOT NULL\n            AND \"GeocodingResultReference\" IS NOT NULL\n            AND \"LocationConfirmedAtUtc\" IS NOT NULL\n        )\n    )\n)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Listings_Location_CoordinatePair",
                table: "Listings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Listings_Location_DisplayNameTrimmedNonBlank",
                table: "Listings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Listings_Location_LatitudeRange",
                table: "Listings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Listings_Location_LongitudeRange",
                table: "Listings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Listings_Location_PrecisionDefined",
                table: "Listings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Listings_Location_ProviderKeyTrimmedNonBlank",
                table: "Listings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Listings_Location_ResultReferenceTrimmedNonBlank",
                table: "Listings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Listings_Location_SnapshotState",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "GeocodedDisplayName",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "GeocodingProviderKey",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "GeocodingResultReference",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "LocationConfirmedAtUtc",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "LocationPrecision",
                table: "Listings");
        }
    }
}
