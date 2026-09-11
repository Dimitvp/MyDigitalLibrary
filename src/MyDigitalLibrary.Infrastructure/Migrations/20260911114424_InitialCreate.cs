using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDigitalLibrary.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "authors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    sort_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    birth_year = table.Column<int>(type: "integer", nullable: true),
                    death_year = table.Column<int>(type: "integer", nullable: true),
                    bio = table.Column<string>(type: "text", nullable: true),
                    external_ids = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_authors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bookstore_listings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    edition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bookstore_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    availability = table.Column<int>(type: "integer", nullable: false),
                    price_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    last_checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    consecutive_failures = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookstore_listings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bookstores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    base_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    adapter_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookstores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "editions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_id = table.Column<Guid>(type: "uuid", nullable: false),
                    format = table.Column<int>(type: "integer", nullable: false),
                    isbn13 = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    publisher = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    translator = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    publication_year = table.Column<int>(type: "integer", nullable: true),
                    page_count = table.Column<int>(type: "integer", nullable: true),
                    cover_type = table.Column<int>(type: "integer", nullable: false),
                    cover_image_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    narrator = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    duration = table.Column<TimeSpan>(type: "interval", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_editions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "genres",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    parent_genre_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_genres", x => x.id);
                    table.ForeignKey(
                        name: "fk_genres_genres_parent_genre_id",
                        column: x => x.parent_genre_id,
                        principalTable: "genres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "import_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    source_file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    stats_total_rows = table.Column<int>(type: "integer", nullable: false),
                    stats_succeeded_rows = table.Column<int>(type: "integer", nullable: false),
                    stats_failed_rows = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "library_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    edition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    format = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    acquired_on = table.Column<DateOnly>(type: "date", nullable: false),
                    acquisition_method = table.Column<int>(type: "integer", nullable: false),
                    acquisition_price_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    acquisition_price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    acquisition_source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    location_room = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    location_shelf = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    location_box = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    personal_note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_library_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "loans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    library_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    borrower_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    lent_on = table.Column<DateOnly>(type: "date", nullable: false),
                    returned_on = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    library_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    location_in_book = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "quotes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    page_or_position = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quotes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reading_goals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    target_books = table.Column<int>(type: "integer", nullable: true),
                    target_pages = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reading_goals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reading_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    library_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    format = table.Column<int>(type: "integer", nullable: false),
                    started_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    ended_on = table.Column<DateOnly>(type: "date", nullable: true),
                    abandon_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reading_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reviews", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "series",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_series", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shelves",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shelves", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "wishlist_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_id = table.Column<Guid>(type: "uuid", nullable: false),
                    preferred_edition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    desired_format = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    max_price_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    max_price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    added_on = table.Column<DateOnly>(type: "date", nullable: false),
                    is_fulfilled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wishlist_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "work_ratings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    rated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_ratings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "works",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    original_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    first_publication_year = table.Column<int>(type: "integer", nullable: true),
                    series_id = table.Column<Guid>(type: "uuid", nullable: true),
                    series_position = table.Column<decimal>(type: "numeric", nullable: true),
                    genre_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_works", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bookstore_listing_price_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookstore_listing_price_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_bookstore_listing_price_history_bookstore_listings_listing_",
                        column: x => x.listing_id,
                        principalTable: "bookstore_listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reading_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reading_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    page_value = table.Column<int>(type: "integer", nullable: true),
                    percent_value = table.Column<decimal>(type: "numeric", nullable: true),
                    position_ticks = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reading_progress", x => x.id);
                    table.CheckConstraint("ck_reading_progress_single_value", "(kind = 'page' AND page_value IS NOT NULL AND percent_value IS NULL AND position_ticks IS NULL)\nOR (kind = 'percent' AND percent_value IS NOT NULL AND page_value IS NULL AND position_ticks IS NULL)\nOR (kind = 'timestamp' AND position_ticks IS NOT NULL AND page_value IS NULL AND percent_value IS NULL)");
                    table.ForeignKey(
                        name: "fk_reading_progress_reading_sessions_reading_session_id",
                        column: x => x.reading_session_id,
                        principalTable: "reading_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shelf_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shelf_id = table.Column<Guid>(type: "uuid", nullable: false),
                    library_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    added_on = table.Column<DateOnly>(type: "date", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shelf_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_shelf_items_shelves_shelf_id",
                        column: x => x.shelf_id,
                        principalTable: "shelves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_authors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_authors", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_authors_works_work_id",
                        column: x => x.work_id,
                        principalTable: "works",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_authors_sort_name",
                table: "authors",
                column: "sort_name");

            migrationBuilder.CreateIndex(
                name: "ix_bookstore_listing_price_history_listing_id",
                table: "bookstore_listing_price_history",
                column: "listing_id");

            migrationBuilder.CreateIndex(
                name: "ix_bookstore_listings_edition_id",
                table: "bookstore_listings",
                column: "edition_id");

            migrationBuilder.CreateIndex(
                name: "ix_bookstore_listings_last_checked_at",
                table: "bookstore_listings",
                column: "last_checked_at");

            migrationBuilder.CreateIndex(
                name: "ix_bookstores_adapter_key",
                table: "bookstores",
                column: "adapter_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_editions_isbn13",
                table: "editions",
                column: "isbn13",
                unique: true,
                filter: "isbn13 IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_editions_work_id",
                table: "editions",
                column: "work_id");

            migrationBuilder.CreateIndex(
                name: "ix_genres_parent_genre_id",
                table: "genres",
                column: "parent_genre_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_jobs_user_id_status",
                table: "import_jobs",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_library_items_user_id_status_format",
                table: "library_items",
                columns: new[] { "user_id", "status", "format" });

            migrationBuilder.CreateIndex(
                name: "ix_loans_library_item_id",
                table: "loans",
                column: "library_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_notes_library_item_id",
                table: "notes",
                column: "library_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotes_work_id",
                table: "quotes",
                column: "work_id");

            migrationBuilder.CreateIndex(
                name: "ix_reading_goals_user_id_year",
                table: "reading_goals",
                columns: new[] { "user_id", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reading_progress_reading_session_id",
                table: "reading_progress",
                column: "reading_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_reading_sessions_user_id_library_item_id",
                table: "reading_sessions",
                columns: new[] { "user_id", "library_item_id" });

            migrationBuilder.CreateIndex(
                name: "ix_reviews_user_id_work_id",
                table: "reviews",
                columns: new[] { "user_id", "work_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shelf_items_shelf_id_library_item_id",
                table: "shelf_items",
                columns: new[] { "shelf_id", "library_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shelves_user_id_name",
                table: "shelves",
                columns: new[] { "user_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tags_user_id_name",
                table: "tags",
                columns: new[] { "user_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wishlist_entries_user_id_is_fulfilled",
                table: "wishlist_entries",
                columns: new[] { "user_id", "is_fulfilled" });

            migrationBuilder.CreateIndex(
                name: "ix_work_authors_author_id",
                table: "work_authors",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_authors_work_id",
                table: "work_authors",
                column: "work_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_ratings_user_id_work_id",
                table: "work_ratings",
                columns: new[] { "user_id", "work_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_works_series_id",
                table: "works",
                column: "series_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "authors");

            migrationBuilder.DropTable(
                name: "bookstore_listing_price_history");

            migrationBuilder.DropTable(
                name: "bookstores");

            migrationBuilder.DropTable(
                name: "editions");

            migrationBuilder.DropTable(
                name: "genres");

            migrationBuilder.DropTable(
                name: "import_jobs");

            migrationBuilder.DropTable(
                name: "library_items");

            migrationBuilder.DropTable(
                name: "loans");

            migrationBuilder.DropTable(
                name: "notes");

            migrationBuilder.DropTable(
                name: "quotes");

            migrationBuilder.DropTable(
                name: "reading_goals");

            migrationBuilder.DropTable(
                name: "reading_progress");

            migrationBuilder.DropTable(
                name: "reviews");

            migrationBuilder.DropTable(
                name: "series");

            migrationBuilder.DropTable(
                name: "shelf_items");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "wishlist_entries");

            migrationBuilder.DropTable(
                name: "work_authors");

            migrationBuilder.DropTable(
                name: "work_ratings");

            migrationBuilder.DropTable(
                name: "bookstore_listings");

            migrationBuilder.DropTable(
                name: "reading_sessions");

            migrationBuilder.DropTable(
                name: "shelves");

            migrationBuilder.DropTable(
                name: "works");
        }
    }
}
