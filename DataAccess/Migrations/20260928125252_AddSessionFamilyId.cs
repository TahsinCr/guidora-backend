using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionFamilyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SessionFamilyId",
                table: "UserSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
WITH RECURSIVE ancestry AS (
    SELECT "Id" AS descendant, "Id" AS ancestor, 0 AS depth FROM "UserSessions"
    UNION ALL
    SELECT ancestry.descendant, parent."Id", ancestry.depth + 1
    FROM ancestry
    JOIN "UserSessions" AS parent ON parent."ReplacedBySessionId" = ancestry.ancestor
), roots AS (
    SELECT DISTINCT ON (descendant) descendant, ancestor
    FROM ancestry ORDER BY descendant, depth DESC
)
UPDATE "UserSessions" AS session
SET "SessionFamilyId" = roots.ancestor
FROM roots WHERE session."Id" = roots.descendant;
""");

            migrationBuilder.AlterColumn<Guid>(
                name: "SessionFamilyId",
                table: "UserSessions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId_SessionFamilyId_RevokedAt",
                table: "UserSessions",
                columns: new[] { "UserId", "SessionFamilyId", "RevokedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserSessions_UserId_SessionFamilyId_RevokedAt",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "SessionFamilyId",
                table: "UserSessions");
        }
    }
}
