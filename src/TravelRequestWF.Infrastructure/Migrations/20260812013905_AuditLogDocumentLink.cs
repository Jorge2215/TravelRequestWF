using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelRequestWF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AuditLogDocumentLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogEntries_TravelRequests_TravelRequestId",
                table: "AuditLogEntries");

            migrationBuilder.AlterColumn<int>(
                name: "TravelRequestId",
                table: "AuditLogEntries",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "RequestDocumentId",
                table: "AuditLogEntries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_RequestDocumentId",
                table: "AuditLogEntries",
                column: "RequestDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogEntries_RequestDocuments_RequestDocumentId",
                table: "AuditLogEntries",
                column: "RequestDocumentId",
                principalTable: "RequestDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogEntries_TravelRequests_TravelRequestId",
                table: "AuditLogEntries",
                column: "TravelRequestId",
                principalTable: "TravelRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogEntries_RequestDocuments_RequestDocumentId",
                table: "AuditLogEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogEntries_TravelRequests_TravelRequestId",
                table: "AuditLogEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogEntries_RequestDocumentId",
                table: "AuditLogEntries");

            migrationBuilder.DropColumn(
                name: "RequestDocumentId",
                table: "AuditLogEntries");

            migrationBuilder.AlterColumn<int>(
                name: "TravelRequestId",
                table: "AuditLogEntries",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogEntries_TravelRequests_TravelRequestId",
                table: "AuditLogEntries",
                column: "TravelRequestId",
                principalTable: "TravelRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
