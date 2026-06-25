using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddListingAgencyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AgencyId",
                table: "Listings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Listings_AgencyId",
                table: "Listings",
                column: "AgencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Listings_Agencies_AgencyId",
                table: "Listings",
                column: "AgencyId",
                principalTable: "Agencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Listings_Agencies_AgencyId",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_AgencyId",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "AgencyId",
                table: "Listings");
        }
    }
}
