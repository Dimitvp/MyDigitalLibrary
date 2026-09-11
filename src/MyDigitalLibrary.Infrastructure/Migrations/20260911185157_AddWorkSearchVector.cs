using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace MyDigitalLibrary.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkSearchVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "works",
                type: "tsvector",
                nullable: true)
                .Annotation("Npgsql:TsVectorConfig", "simple")
                .Annotation("Npgsql:TsVectorProperties", new[] { "title", "original_title", "description" });

            migrationBuilder.CreateIndex(
                name: "ix_works_search_vector",
                table: "works",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_works_search_vector",
                table: "works");

            migrationBuilder.DropColumn(
                name: "search_vector",
                table: "works");
        }
    }
}
