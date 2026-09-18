using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trust_Finance.Migrations
{
    /// <summary>
    /// Categories used to be global: every signed-in user saw, edited and deleted the same
    /// list, and deleting one cascaded into other people's transactions. This gives every
    /// category an owner and makes the slug unique per user instead of globally.
    ///
    /// The existing rows have no owner, so they are attributed from the transactions that
    /// reference them: the lowest-numbered user keeps the original row, and every other
    /// user who had transactions in a shared category gets their own copy with their
    /// transactions repointed to it. Nothing is lost.
    /// </summary>
    public partial class CategoryBelongsToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Categories_Slug",
                table: "Categories");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Categories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // --- backfill -----------------------------------------------------------
            // Must run before the foreign key is added: every row still has UserId = 0,
            // which matches no user.
            migrationBuilder.Sql(@"
                -- 1. The lowest-numbered user with transactions in a category keeps it.
                UPDATE c
                SET    c.UserId = owner.UserId
                FROM   Categories c
                JOIN   (SELECT CategoryId, MIN(UserId) AS UserId
                        FROM   Transactions
                        GROUP  BY CategoryId) owner ON owner.CategoryId = c.Id;

                -- 2. Every other user who used that category gets their own copy, and
                --    their transactions are repointed to it.
                DECLARE @copies TABLE (NewId int, SourceId int, UserId int);

                MERGE INTO Categories AS target
                USING (SELECT DISTINCT t.UserId, c.Id AS SourceId, c.Name, c.Slug
                       FROM   Transactions t
                       JOIN   Categories c ON c.Id = t.CategoryId
                       WHERE  t.UserId <> c.UserId) AS source
                ON 1 = 0
                WHEN NOT MATCHED BY TARGET THEN
                    INSERT (Name, Slug, UserId)
                    VALUES (source.Name, source.Slug, source.UserId)
                OUTPUT inserted.Id, source.SourceId, source.UserId
                INTO @copies (NewId, SourceId, UserId);

                UPDATE t
                SET    t.CategoryId = copy.NewId
                FROM   Transactions t
                JOIN   @copies copy ON copy.SourceId = t.CategoryId
                                   AND copy.UserId  = t.UserId;

                -- 3. A category nobody ever used cannot be attributed. Park it with the
                --    oldest account rather than dropping it; if there are no accounts at
                --    all there is nothing that could own it.
                IF EXISTS (SELECT 1 FROM Users)
                    UPDATE Categories
                    SET    UserId = (SELECT MIN(Id) FROM Users)
                    WHERE  UserId = 0;

                DELETE FROM Categories WHERE UserId = 0;
            ");
            // ------------------------------------------------------------------------

            migrationBuilder.CreateIndex(
                name: "IX_Categories_UserId_Slug",
                table: "Categories",
                columns: new[] { "UserId", "Slug" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Users_UserId",
                table: "Categories",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Users_UserId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_UserId_Slug",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Categories");

            // Slug is about to be globally unique again, so the per-user copies created on
            // the way up have to be collapsed back into one row per slug. Transactions
            // follow the surviving row.
            migrationBuilder.Sql(@"
                UPDATE t
                SET    t.CategoryId = keep.Id
                FROM   Transactions t
                JOIN   Categories c ON c.Id = t.CategoryId
                JOIN   (SELECT Slug, MIN(Id) AS Id
                        FROM   Categories
                        GROUP  BY Slug) keep ON keep.Slug = c.Slug
                WHERE  t.CategoryId <> keep.Id;

                DELETE c
                FROM   Categories c
                JOIN   (SELECT Slug, MIN(Id) AS Id
                        FROM   Categories
                        GROUP  BY Slug) keep ON keep.Slug = c.Slug
                WHERE  c.Id <> keep.Id;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);
        }
    }
}
