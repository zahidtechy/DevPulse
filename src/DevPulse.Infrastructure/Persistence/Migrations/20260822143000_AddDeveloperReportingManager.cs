using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeveloperReportingManager : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReportingManagerDeveloperId",
                table: "Developers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Developers_ReportingManagerDeveloperId",
                table: "Developers",
                column: "ReportingManagerDeveloperId");

            migrationBuilder.AddForeignKey(
                name: "FK_Developers_Developers_ReportingManagerDeveloperId",
                table: "Developers",
                column: "ReportingManagerDeveloperId",
                principalTable: "Developers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Developers_Developers_ReportingManagerDeveloperId",
                table: "Developers");

            migrationBuilder.DropIndex(
                name: "IX_Developers_ReportingManagerDeveloperId",
                table: "Developers");

            migrationBuilder.DropColumn(
                name: "ReportingManagerDeveloperId",
                table: "Developers");
        }
    }
}
