using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFinance.Data.Migrations
{
    /// <inheritdoc />
    public partial class CorporateActionsAndPayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CorporateActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ticker = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Factor = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: false),
                    BonusUnitCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorporateActions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ticker = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    WithheldTax = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payouts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateActions_UserId_Ticker_Date",
                table: "CorporateActions",
                columns: new[] { "UserId", "Ticker", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_UserId_PaymentDate",
                table: "Payouts",
                columns: new[] { "UserId", "PaymentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_UserId_Ticker",
                table: "Payouts",
                columns: new[] { "UserId", "Ticker" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorporateActions");

            migrationBuilder.DropTable(
                name: "Payouts");
        }
    }
}
