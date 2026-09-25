using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class CommunityVerificationAndOpenReportUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
WITH ranked AS (
    SELECT "Id", ROW_NUMBER() OVER (
        PARTITION BY "ProductId", "ReportedByUserId"
        ORDER BY "CreatedAt", "Id") AS position
    FROM "ProductReports"
    WHERE "ReportStatusId" = 1
)
UPDATE "ProductReports" AS report
SET "ReportStatusId" = 3, "ResolvedAt" = COALESCE(report."ResolvedAt", NOW())
FROM ranked
WHERE report."Id" = ranked."Id" AND ranked.position > 1;
""");

            migrationBuilder.CreateTable(
                name: "ProductVerificationVotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Approved = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVerificationVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductVerificationVotes_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductVerificationVotes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "OperationClaims",
                columns: new[] { "Id", "Name" },
                values: new object[] { 18, "Product.Verify.Community.Vote" });

            migrationBuilder.InsertData(
                table: "RoleOperationClaims",
                columns: new[] { "Id", "OperationClaimId", "RoleId" },
                values: new object[] { 31, 18, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_ProductReports_ProductId_ReportedByUserId",
                table: "ProductReports",
                columns: new[] { "ProductId", "ReportedByUserId" },
                unique: true,
                filter: "\"ReportStatusId\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVerificationVotes_ProductId_Approved",
                table: "ProductVerificationVotes",
                columns: new[] { "ProductId", "Approved" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVerificationVotes_ProductId_UserId",
                table: "ProductVerificationVotes",
                columns: new[] { "ProductId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVerificationVotes_UserId",
                table: "ProductVerificationVotes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductVerificationVotes");

            migrationBuilder.DropIndex(
                name: "IX_ProductReports_ProductId_ReportedByUserId",
                table: "ProductReports");

            migrationBuilder.DeleteData(
                table: "RoleOperationClaims",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "OperationClaims",
                keyColumn: "Id",
                keyValue: 18);
        }
    }
}
