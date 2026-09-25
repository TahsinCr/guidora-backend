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

            migrationBuilder.Sql("UPDATE \"UserSessions\" SET \"AbsoluteExpiresAt\" = \"CreatedAt\" + INTERVAL '180 days';");
            migrationBuilder.Sql("UPDATE \"ProductEditProposals\" AS proposal SET \"BaseProductVersion\" = product.xmin::text::bigint FROM \"Products\" AS product WHERE product.\"ProductId\" = proposal.\"ProductId\";");

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
