using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Trader.MarketData.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "instrument_types",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instrument_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "instruments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ticker = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Market = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InstrumentTypeId = table.Column<int>(type: "integer", nullable: true),
                    ProviderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DiscoveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instruments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_instruments_instrument_types_InstrumentTypeId",
                        column: x => x.InstrumentTypeId,
                        principalTable: "instrument_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "quote_daily",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstrumentId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Open = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    High = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Low = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Close = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Volume = table.Column<decimal>(type: "numeric(28,6)", precision: 28, scale: 6, nullable: false),
                    PreviousClose = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Change = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    ChangePercent = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Settlement = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ProviderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_daily", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quote_daily_instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quote_intraday",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstrumentId = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Volume = table.Column<decimal>(type: "numeric(28,6)", precision: 28, scale: 6, nullable: false),
                    Open = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    High = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Low = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Change = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    ChangePercent = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ProviderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_intraday", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quote_intraday_instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_instrument_types_Code",
                table: "instrument_types",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_instruments_InstrumentTypeId",
                table: "instruments",
                column: "InstrumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_instruments_Ticker_Market",
                table: "instruments",
                columns: new[] { "Ticker", "Market" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quote_daily_Date",
                table: "quote_daily",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_quote_daily_InstrumentId_Date_Settlement",
                table: "quote_daily",
                columns: new[] { "InstrumentId", "Date", "Settlement" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quote_intraday_InstrumentId_Timestamp",
                table: "quote_intraday",
                columns: new[] { "InstrumentId", "Timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quote_intraday_Timestamp",
                table: "quote_intraday",
                column: "Timestamp");

            // Convert to TimescaleDB hypertables (requires TimescaleDB extension).
            // These are best-effort — the migration continues even if TimescaleDB
            // is not installed (e.g. plain PostgreSQL in development).
            migrationBuilder.Sql(
                "SELECT create_hypertable('quote_daily', 'date', if_not_exists => TRUE);",
                suppressTransaction: true);
            migrationBuilder.Sql(
                "SELECT create_hypertable('quote_intraday', 'timestamp', if_not_exists => TRUE);",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quote_daily");

            migrationBuilder.DropTable(
                name: "quote_intraday");

            migrationBuilder.DropTable(
                name: "instruments");

            migrationBuilder.DropTable(
                name: "instrument_types");
        }
    }
}
