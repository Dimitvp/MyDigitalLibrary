using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDigitalLibrary.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWishlistEntryIsOutOfStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_out_of_stock",
                table: "wishlist_entries",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_out_of_stock",
                table: "wishlist_entries");
        }
    }
}
