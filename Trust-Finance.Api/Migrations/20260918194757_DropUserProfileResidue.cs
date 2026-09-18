using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trust_Finance.Migrations
{
    /// <summary>
    /// Users carried three columns that nothing in the product used.
    ///
    /// Slug is a blog idea: it exists so a profile can have a URL, and this
    /// application has no profile page. Image was never rendered anywhere, and
    /// registration stopped asking for it. Role backed an admin area whose four
    /// endpoints no client ever called -- in a personal ledger there is nobody
    /// to administer, so the role had nothing left to protect.
    ///
    /// Down restores the columns but not their contents, which the drop discards.
    /// </summary>
    public partial class DropUserProfileResidue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Image",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Image",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "user");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}
