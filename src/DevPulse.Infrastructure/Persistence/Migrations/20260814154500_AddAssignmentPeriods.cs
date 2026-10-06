using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WorkRole",
                table: "Developers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TaskAssignmentPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DeveloperId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UnassignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAssignmentPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskAssignmentPeriods_ClickUpAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "ClickUpAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskAssignmentPeriods_Developers_DeveloperId",
                        column: x => x.DeveloperId,
                        principalTable: "Developers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO TaskAssignmentPeriods (Id, AccountId, TaskId, DeveloperId, AssignedAtUtc, UnassignedAtUtc)
                SELECT
                    NEWID(),
                    AccountId,
                    TaskId,
                    DeveloperId,
                    CASE
                        WHEN DateCreated IS NULL OR DateCreated <= 0 THEN SyncedAtUtc
                        ELSE DATEADD(MILLISECOND, CAST(DateCreated % 1000 AS int), DATEADD(SECOND, CAST(DateCreated / 1000 AS int), '1970-01-01T00:00:00'))
                    END,
                    NULL
                FROM SyncedTasks;
                """);

            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY AccountId, TaskId
                               ORDER BY CASE WHEN IsCompleted = 1 THEN 0 ELSE 1 END, SyncedAtUtc DESC
                           ) AS rn
                    FROM SyncedTasks
                )
                DELETE FROM SyncedTasks WHERE Id IN (SELECT Id FROM ranked WHERE rn > 1);
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_SyncedTasks_Developers_DeveloperId",
                table: "SyncedTasks");

            migrationBuilder.DropIndex(
                name: "IX_SyncedTasks_DeveloperId_AccountId_TaskId",
                table: "SyncedTasks");

            // Present on some databases from an earlier local index that was never captured in migrations.
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_SyncedTasks_DeveloperId_IsCompleted'
                      AND object_id = OBJECT_ID(N'dbo.SyncedTasks')
                )
                    DROP INDEX [IX_SyncedTasks_DeveloperId_IsCompleted] ON [SyncedTasks];
                """);

            migrationBuilder.DropColumn(
                name: "DeveloperId",
                table: "SyncedTasks");

            migrationBuilder.CreateIndex(
                name: "IX_SyncedTasks_AccountId_TaskId",
                table: "SyncedTasks",
                columns: new[] { "AccountId", "TaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentPeriods_AccountId",
                table: "TaskAssignmentPeriods",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentPeriods_DeveloperId",
                table: "TaskAssignmentPeriods",
                column: "DeveloperId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentPeriods_DeveloperId_AssignedAtUtc_UnassignedAtUtc",
                table: "TaskAssignmentPeriods",
                columns: new[] { "DeveloperId", "AssignedAtUtc", "UnassignedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentPeriods_AccountId_TaskId_DeveloperId_UnassignedAtUtc",
                table: "TaskAssignmentPeriods",
                columns: new[] { "AccountId", "TaskId", "DeveloperId", "UnassignedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentPeriods_Open",
                table: "TaskAssignmentPeriods",
                columns: new[] { "AccountId", "TaskId", "DeveloperId" },
                unique: true,
                filter: "[UnassignedAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskAssignmentPeriods");

            migrationBuilder.DropIndex(
                name: "IX_SyncedTasks_AccountId_TaskId",
                table: "SyncedTasks");

            migrationBuilder.DropColumn(
                name: "WorkRole",
                table: "Developers");

            migrationBuilder.AddColumn<Guid>(
                name: "DeveloperId",
                table: "SyncedTasks",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_SyncedTasks_DeveloperId_AccountId_TaskId",
                table: "SyncedTasks",
                columns: new[] { "DeveloperId", "AccountId", "TaskId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SyncedTasks_Developers_DeveloperId",
                table: "SyncedTasks",
                column: "DeveloperId",
                principalTable: "Developers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
