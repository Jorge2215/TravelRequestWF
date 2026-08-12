using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelRequestWF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Stage4WorkflowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "TravelRequests",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "AuditLogEntries",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "TravelRequests");

            migrationBuilder.DropColumn(
                name: "Details",
                table: "AuditLogEntries");
        }
    }
}
