using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddListingCreatedByUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Listings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Listings_CreatedByUserId",
                table: "Listings",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Listings_Users_CreatedByUserId",
                table: "Listings",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Listings_Users_CreatedByUserId",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_CreatedByUserId",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Listings");
        }
    }
}
