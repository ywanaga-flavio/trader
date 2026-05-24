using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trader.News.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsAnalysisFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_news_items_Classification",
                table: "news_items");

            migrationBuilder.DropColumn(
                name: "Classification",
                table: "news_items");

            migrationBuilder.RenameColumn(
                name: "ValuationScore",
                table: "news_items",
                newName: "SentimentScore");

            migrationBuilder.RenameColumn(
                name: "ValuationId",
                table: "news_items",
                newName: "SentimentId");

            migrationBuilder.AddColumn<int>(
                name: "ClassificationId",
                table: "news_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ClassificationScore",
                table: "news_items",
                type: "double precision",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_news_items_ClassificationId",
                table: "news_items",
                column: "ClassificationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_news_items_ClassificationId",
                table: "news_items");

            migrationBuilder.DropColumn(
                name: "ClassificationId",
                table: "news_items");

            migrationBuilder.DropColumn(
                name: "ClassificationScore",
                table: "news_items");

            migrationBuilder.RenameColumn(
                name: "SentimentScore",
                table: "news_items",
                newName: "ValuationScore");

            migrationBuilder.RenameColumn(
                name: "SentimentId",
                table: "news_items",
                newName: "ValuationId");

            migrationBuilder.AddColumn<int>(
                name: "Classification",
                table: "news_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_news_items_Classification",
                table: "news_items",
                column: "Classification");
        }
    }
}
