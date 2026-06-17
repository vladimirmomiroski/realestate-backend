using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddListingCommonDetailsAndMunicipality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Municipality",
                table: "ListingTranslations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BalconyCount",
                table: "Listings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Condition",
                table: "Listings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "FurnishingStatus",
                table: "Listings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<bool>(
                name: "HasBasement",
                table: "Listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeatingType",
                table: "Listings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<bool>(
                name: "IsExchangePossible",
                table: "Listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Orientation",
                table: "Listings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<int>(
                name: "ParkingSpaces",
                table: "Listings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearRenovated",
                table: "Listings",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Municipality",
                table: "ListingTranslations");

            migrationBuilder.DropColumn(
                name: "BalconyCount",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "Condition",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "FurnishingStatus",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "HasBasement",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "HeatingType",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "IsExchangePossible",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "Orientation",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "ParkingSpaces",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "YearRenovated",
                table: "Listings");
        }
    }
}
