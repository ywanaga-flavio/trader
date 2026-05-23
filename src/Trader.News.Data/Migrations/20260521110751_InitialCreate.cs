using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Trader.News.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "news_sources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Uri = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PasswordEncrypted = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SearchIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    LastExecution = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_news_sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "news_items",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceId = table.Column<int>(type: "integer", nullable: false),
                    Uri = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NewsDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Classification = table.Column<int>(type: "integer", nullable: false),
                    ValuationId = table.Column<int>(type: "integer", nullable: true),
                    ValuationScore = table.Column<double>(type: "double precision", precision: 5, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_news_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_news_items_news_sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "news_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_news_items_Classification",
                table: "news_items",
                column: "Classification");

            migrationBuilder.CreateIndex(
                name: "IX_news_items_NewsDate",
                table: "news_items",
                column: "NewsDate");

            migrationBuilder.CreateIndex(
                name: "IX_news_items_SourceId",
                table: "news_items",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_news_items_SourceId_Uri",
                table: "news_items",
                columns: new[] { "SourceId", "Uri" },
                unique: true,
                filter: "uri IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_news_sources_IsEnabled",
                table: "news_sources",
                column: "IsEnabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "news_items");

            migrationBuilder.DropTable(
                name: "news_sources");
        }
    }
}
