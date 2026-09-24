using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditRevertPermissionAndProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RevertsAuditLogId",
                table: "AuditLogs",
                type: "bigint",
                nullable: true);

            migrationBuilder.InsertData(
                table: "OperationClaims",
                columns: new[] { "Id", "Name" },
                values: new object[] { 14, "Audit.Revert" });

            migrationBuilder.InsertData(
                table: "RoleOperationClaims",
                columns: new[] { "Id", "OperationClaimId", "RoleId" },
                values: new object[] { 24, 14, 3 });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_RevertsAuditLogId",
                table: "AuditLogs",
                column: "RevertsAuditLogId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_AuditLogs_RevertsAuditLogId",
                table: "AuditLogs",
                column: "RevertsAuditLogId",
                principalTable: "AuditLogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_AuditLogs_RevertsAuditLogId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_RevertsAuditLogId",
                table: "AuditLogs");

            migrationBuilder.DeleteData(
                table: "RoleOperationClaims",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "OperationClaims",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DropColumn(
                name: "RevertsAuditLogId",
                table: "AuditLogs");
        }
    }
}
