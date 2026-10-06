using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskAssignmentPeriods_AccountId",
                table: "TaskAssignmentPeriods");

            migrationBuilder.DropIndex(
                name: "IX_TaskAssignmentPeriods_DeveloperId",
                table: "TaskAssignmentPeriods");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentPeriods_AccountId",
                table: "TaskAssignmentPeriods",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentPeriods_DeveloperId",
                table: "TaskAssignmentPeriods",
                column: "DeveloperId");
        }
    }
}
