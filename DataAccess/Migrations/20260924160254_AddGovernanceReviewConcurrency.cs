using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddGovernanceReviewConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_RevertsAuditLogId",
                table: "AuditLogs");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ProductVerifications",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ProductReports",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ProductEditProposals",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "UX_AuditLogs_RevertsAuditLogId",
                table: "AuditLogs",
                column: "RevertsAuditLogId",
                unique: true,
                filter: "\"RevertsAuditLogId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_AuditLogs_RevertsAuditLogId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ProductVerifications");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ProductReports");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ProductEditProposals");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_RevertsAuditLogId",
                table: "AuditLogs",
                column: "RevertsAuditLogId");
        }
    }
}
