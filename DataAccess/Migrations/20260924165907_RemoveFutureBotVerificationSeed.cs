using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFutureBotVerificationSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "VerificationTypes" AS verification_type
                WHERE verification_type."Id" = 3
                  AND verification_type."Code" = 'bot'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "ProductVerifications" AS product_verification
                      WHERE product_verification."VerificationTypeId" = verification_type."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "VerificationTypes" ("Id", "Code", "Name")
                SELECT 3, 'bot', 'Bot verified'
                WHERE NOT EXISTS (SELECT 1 FROM "VerificationTypes" WHERE "Id" = 3)
                  AND NOT EXISTS (SELECT 1 FROM "VerificationTypes" WHERE "Code" = 'bot');
                """);
        }
    }
}
