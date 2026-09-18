using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trust_Finance.Migrations
{
    /// <summary>
    /// A transaction had no direction: income and expenses were both stored as a
    /// positive Amount, so the dashboard could only ever report volume, never a
    /// balance. Type carries the sign from now on.
    ///
    /// Existing rows predate the distinction and are set to Expense. In a log kept
    /// without a type almost every entry is a payment, and correcting the handful of
    /// deposits afterwards is far less work than the other way round.
    /// </summary>
    public partial class TransactionHasType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 0 is deliberately not one of Income (1) or Expense (2): a row that
            // reaches the database without a type stays visibly wrong instead of
            // passing as a real one.
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Transactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE Transactions SET Type = 2 WHERE Type = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "Transactions");
        }
    }
}
