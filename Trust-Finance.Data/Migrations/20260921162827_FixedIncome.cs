using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFinance.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixedIncome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FixedIncomeInvestments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Issuer = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Rate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    Principal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PurchaseDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    MaturityDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    HasDailyLiquidity = table.Column<bool>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    RedeemedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixedIncomeInvestments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FixedIncomeInvestments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FixedIncomeInvestments_UserId_MaturityDate",
                table: "FixedIncomeInvestments",
                columns: new[] { "UserId", "MaturityDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FixedIncomeInvestments");
        }
    }
}
