using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDigitalLibrary.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManualFieldOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "field_overrides",
                table: "works",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "field_overrides",
                table: "editions",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "field_overrides",
                table: "works");

            migrationBuilder.DropColumn(
                name: "field_overrides",
                table: "editions");
        }
    }
}
