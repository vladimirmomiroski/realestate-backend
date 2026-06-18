using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddListingPropertyDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Floor",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "TotalFloors",
                table: "Listings");

            migrationBuilder.CreateTable(
                name: "ListingApartmentDetails",
                columns: table => new
                {
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApartmentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Unknown"),
                    Floor = table.Column<int>(type: "integer", nullable: true),
                    TotalFloors = table.Column<int>(type: "integer", nullable: true),
                    HasElevator = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingApartmentDetails", x => x.ListingId);
                    table.ForeignKey(
                        name: "FK_ListingApartmentDetails_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ListingHouseDetails",
                columns: table => new
                {
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Unknown"),
                    NumberOfFloors = table.Column<int>(type: "integer", nullable: true),
                    YardAreaSquareMeters = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingHouseDetails", x => x.ListingId);
                    table.ForeignKey(
                        name: "FK_ListingHouseDetails_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ListingApartmentDetails");

            migrationBuilder.DropTable(
                name: "ListingHouseDetails");

            migrationBuilder.AddColumn<int>(
                name: "Floor",
                table: "Listings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalFloors",
                table: "Listings",
                type: "integer",
                nullable: true);
        }
    }
}
