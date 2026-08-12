using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelRequestWF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RequestDocumentRestrictDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RequestDocuments_TravelRequests_TravelRequestId",
                table: "RequestDocuments");

            migrationBuilder.AddForeignKey(
                name: "FK_RequestDocuments_TravelRequests_TravelRequestId",
                table: "RequestDocuments",
                column: "TravelRequestId",
                principalTable: "TravelRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RequestDocuments_TravelRequests_TravelRequestId",
                table: "RequestDocuments");

            migrationBuilder.AddForeignKey(
                name: "FK_RequestDocuments_TravelRequests_TravelRequestId",
                table: "RequestDocuments",
                column: "TravelRequestId",
                principalTable: "TravelRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
