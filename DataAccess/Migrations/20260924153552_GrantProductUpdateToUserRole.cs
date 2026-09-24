using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class GrantProductUpdateToUserRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "RoleOperationClaims",
                columns: new[] { "Id", "OperationClaimId", "RoleId" },
                values: new object[] { 25, 2, 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RoleOperationClaims",
                keyColumn: "Id",
                keyValue: 25);

            // xmin is a PostgreSQL system column; it is not created or removed by migrations.
        }
    }
}
