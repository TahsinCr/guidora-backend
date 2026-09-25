using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddAbsoluteSessionExpiryProposalVersionAndReplayGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AbsoluteExpiresAt",
                table: "UserSessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ReplayDetectedAt",
                table: "UserSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "BaseProductVersion",
                table: "ProductEditProposals",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql("""
WITH RECURSIVE ancestry AS (
    SELECT "Id" AS descendant, "Id" AS ancestor, "CreatedAt" FROM "UserSessions"
    UNION ALL
    SELECT ancestry.descendant, parent."Id", parent."CreatedAt"
    FROM ancestry
    JOIN "UserSessions" AS parent ON parent."ReplacedBySessionId" = ancestry.ancestor
), roots AS (
    SELECT descendant, MIN("CreatedAt") AS first_created FROM ancestry GROUP BY descendant
)
UPDATE "UserSessions" AS session
SET "AbsoluteExpiresAt" = roots.first_created + INTERVAL '180 days'
FROM roots WHERE session."Id" = roots.descendant;
""");
            // The base revision of legacy proposals is unknowable. Their zero value
            // deliberately makes pending proposals conflict until resubmitted.

            migrationBuilder.AlterColumn<DateTime>(
                name: "AbsoluteExpiresAt",
                table: "UserSessions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<long>(
                name: "BaseProductVersion",
                table: "ProductEditProposals",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldDefaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbsoluteExpiresAt",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "ReplayDetectedAt",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "BaseProductVersion",
                table: "ProductEditProposals");
        }
    }
}
