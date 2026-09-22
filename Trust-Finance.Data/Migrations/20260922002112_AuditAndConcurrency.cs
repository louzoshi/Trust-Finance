using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFinance.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuditAndConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedSignInCount",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockedOutUntil",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Trades",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "RecurringTransactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Payouts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "FixedIncomeInvestments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "CorporateActions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Budgets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Entity = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    EntityId = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    Changes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_UserId_OccurredAt",
                table: "AuditEntries",
                columns: new[] { "UserId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "FailedSignInCount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockedOutUntil",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Trades");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "RecurringTransactions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "FixedIncomeInvestments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "CorporateActions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Budgets");
        }
    }
}
