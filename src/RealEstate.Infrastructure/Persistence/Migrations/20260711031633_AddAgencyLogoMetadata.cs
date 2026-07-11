using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgencyLogoMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoContentType",
                table: "Agencies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LogoSizeBytes",
                table: "Agencies",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoStoredFileName",
                table: "Agencies",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoContentType",
                table: "Agencies");

            migrationBuilder.DropColumn(
                name: "LogoSizeBytes",
                table: "Agencies");

            migrationBuilder.DropColumn(
                name: "LogoStoredFileName",
                table: "Agencies");
        }
    }
}
