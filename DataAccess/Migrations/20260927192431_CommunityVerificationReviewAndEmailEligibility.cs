using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class CommunityVerificationReviewAndEmailEligibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductVerificationVotes_ProductId_Approved",
                table: "ProductVerificationVotes");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerifiedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EligibleAtCast",
                table: "ProductVerificationVotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OriginClientHash",
                table: "ProductVerificationVotes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginIpHash",
                table: "ProductVerificationVotes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommunityVerificationReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    ReachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityVerificationReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityVerificationReviews_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommunityVerificationReviews_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmailVerificationChallenges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailVerificationChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailVerificationChallenges_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmailVerifiedAt",
                table: "Users",
                column: "EmailVerifiedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVerificationVotes_ProductId_Approved_EligibleAtCast",
                table: "ProductVerificationVotes",
                columns: new[] { "ProductId", "Approved", "EligibleAtCast" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityVerificationReviews_ProductId",
                table: "CommunityVerificationReviews",
                column: "ProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityVerificationReviews_ReviewedByUserId",
                table: "CommunityVerificationReviews",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityVerificationReviews_Status_ReachedAt",
                table: "CommunityVerificationReviews",
                columns: new[] { "Status", "ReachedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationChallenges_UserId",
                table: "EmailVerificationChallenges",
                column: "UserId",
                unique: true);

            // Previous five-vote decisions did not pass the new eligibility
            // and human review rules. Preserve the records as revoked history.
            migrationBuilder.Sql("""
INSERT INTO "AuditLogs" ("ActorUserId", "EntityType", "EntityId", "Action", "OldValues", "NewValues", "CreatedAt")
SELECT NULL, 'ProductVerification', verification."ProductId"::text,
       'CommunityVerificationPolicyMigration',
       jsonb_build_object('Status', 'Active', 'VerificationId', verification."Id",
                          'Note', verification."Note", 'VerifiedByUserId', verification."VerifiedByUserId",
                          'VerifiedAt', verification."VerifiedAt"),
       jsonb_build_object('Status', 'Revoked', 'Reason', 'V1 voter eligibility and moderator review policy'),
       NOW()
FROM "ProductVerifications" AS verification
JOIN "VerificationTypes" AS type ON type."Id" = verification."VerificationTypeId"
WHERE type."Code" = 'community' AND verification."RevokedAt" IS NULL;

UPDATE "ProductVerifications" AS verification
SET "RevokedAt" = NOW(),
    "Note" = 'Revoked by V1 voter eligibility and moderator review policy migration.'
FROM "VerificationTypes" AS type
WHERE verification."VerificationTypeId" = type."Id"
  AND type."Code" = 'community' AND verification."RevokedAt" IS NULL;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
UPDATE "ProductVerifications" AS verification
SET "RevokedAt" = NULL,
    "Note" = COALESCE(audit."OldValues"->>'Note', '')
FROM "AuditLogs" AS audit
WHERE audit."Action" = 'CommunityVerificationPolicyMigration'
  AND audit."EntityId" = verification."ProductId"::text
  AND (audit."OldValues"->>'VerificationId')::integer = verification."Id"
  AND verification."Note" = 'Revoked by V1 voter eligibility and moderator review policy migration.';
""");
            migrationBuilder.DropTable(
                name: "CommunityVerificationReviews");

            migrationBuilder.DropTable(
                name: "EmailVerificationChallenges");

            migrationBuilder.DropIndex(
                name: "IX_Users_EmailVerifiedAt",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_ProductVerificationVotes_ProductId_Approved_EligibleAtCast",
                table: "ProductVerificationVotes");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailVerifiedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EligibleAtCast",
                table: "ProductVerificationVotes");

            migrationBuilder.DropColumn(
                name: "OriginClientHash",
                table: "ProductVerificationVotes");

            migrationBuilder.DropColumn(
                name: "OriginIpHash",
                table: "ProductVerificationVotes");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVerificationVotes_ProductId_Approved",
                table: "ProductVerificationVotes",
                columns: new[] { "ProductId", "Approved" });
        }
    }
}
