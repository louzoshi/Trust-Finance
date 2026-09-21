using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFinance.Data.Migrations
{
    /// <inheritdoc />
    public partial class InvestingAndPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Budgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MonthlyLimit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Budgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Budgets_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Budgets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PriceAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ticker = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Class = table.Column<int>(type: "INTEGER", nullable: false),
                    Direction = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: false),
                    TriggeredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TriggeredPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: true),
                    Dismissed = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceAlerts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ticker = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Class = table.Column<int>(type: "INTEGER", nullable: false),
                    Side = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: false),
                    Fees = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trades_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Theme = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Density = table.Column<int>(type: "INTEGER", nullable: false),
                    StartPage = table.Column<int>(type: "INTEGER", nullable: false),
                    AccentColor = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    PrivacyMode = table.Column<bool>(type: "INTEGER", nullable: false),
                    MarketDataToken = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    QuoteRefreshMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    NotifyPriceAlerts = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotifyBudgetOverruns = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotifyContributionGoal = table.Column<bool>(type: "INTEGER", nullable: false),
                    MonthlyContributionGoal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    EmergencyFundGoal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Watchlist",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ticker = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Class = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Watchlist", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Watchlist_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_CategoryId",
                table: "Budgets",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_UserId_CategoryId",
                table: "Budgets",
                columns: new[] { "UserId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceAlerts_UserId_TriggeredAt",
                table: "PriceAlerts",
                columns: new[] { "UserId", "TriggeredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Trades_UserId_Date",
                table: "Trades",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Trades_UserId_Ticker",
                table: "Trades",
                columns: new[] { "UserId", "Ticker" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_UserId",
                table: "UserSettings",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Watchlist_UserId_Ticker",
                table: "Watchlist",
                columns: new[] { "UserId", "Ticker" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Budgets");

            migrationBuilder.DropTable(
                name: "PriceAlerts");

            migrationBuilder.DropTable(
                name: "Trades");

            migrationBuilder.DropTable(
                name: "UserSettings");

            migrationBuilder.DropTable(
                name: "Watchlist");
        }
    }
}
