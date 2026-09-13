using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDigitalLibrary.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFollowedBookSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "followed_book_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    added_on = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_followed_book_sources", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_followed_book_sources_user_id_url",
                table: "followed_book_sources",
                columns: new[] { "user_id", "url" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "followed_book_sources");
        }
    }
}
