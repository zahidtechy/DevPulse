using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKpiSyncTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KpiSyncRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TasksUpserted = table.Column<int>(type: "int", nullable: false),
                    DeveloperCount = table.Column<int>(type: "int", nullable: false),
                    AccountCount = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TriggeredManually = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KpiSyncRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncedTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeveloperId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FolderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TaskId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TaskName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ListName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DateCreated = table.Column<long>(type: "bigint", nullable: true),
                    DateDone = table.Column<long>(type: "bigint", nullable: true),
                    DueDate = table.Column<long>(type: "bigint", nullable: true),
                    CompletionDays = table.Column<double>(type: "float", nullable: true),
                    IsSubtask = table.Column<bool>(type: "bit", nullable: false),
                    ParentTaskId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ParentTaskName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TaskType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    SyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncedTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncedTasks_ClickUpAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "ClickUpAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SyncedTasks_Developers_DeveloperId",
                        column: x => x.DeveloperId,
                        principalTable: "Developers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeveloperKpiSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SyncRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeveloperId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DeveloperName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    TotalTasks = table.Column<int>(type: "int", nullable: false),
                    CompletedCount = table.Column<int>(type: "int", nullable: false),
                    InProgressCount = table.Column<int>(type: "int", nullable: false),
                    ChildTaskCount = table.Column<int>(type: "int", nullable: false),
                    WorkspaceCount = table.Column<int>(type: "int", nullable: false),
                    ProjectCount = table.Column<int>(type: "int", nullable: false),
                    OverdueCount = table.Column<int>(type: "int", nullable: false),
                    OnTimeCompletedCount = table.Column<int>(type: "int", nullable: false),
                    AverageCompletionDays = table.Column<double>(type: "float", nullable: true),
                    CompletionRate = table.Column<double>(type: "float", nullable: false),
                    OnTimeRate = table.Column<double>(type: "float", nullable: true),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeveloperKpiSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeveloperKpiSnapshots_Developers_DeveloperId",
                        column: x => x.DeveloperId,
                        principalTable: "Developers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeveloperKpiSnapshots_KpiSyncRuns_SyncRunId",
                        column: x => x.SyncRunId,
                        principalTable: "KpiSyncRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperKpiSnapshots_DeveloperId",
                table: "DeveloperKpiSnapshots",
                column: "DeveloperId");

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperKpiSnapshots_FromDate_ToDate_DeveloperId",
                table: "DeveloperKpiSnapshots",
                columns: new[] { "FromDate", "ToDate", "DeveloperId" });

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperKpiSnapshots_SyncRunId",
                table: "DeveloperKpiSnapshots",
                column: "SyncRunId");

            migrationBuilder.CreateIndex(
                name: "IX_KpiSyncRuns_StartedAtUtc",
                table: "KpiSyncRuns",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SyncedTasks_AccountId_IsCompleted_DateCreated",
                table: "SyncedTasks",
                columns: new[] { "AccountId", "IsCompleted", "DateCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncedTasks_AccountId_IsCompleted_DateDone",
                table: "SyncedTasks",
                columns: new[] { "AccountId", "IsCompleted", "DateDone" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncedTasks_DeveloperId_AccountId_TaskId",
                table: "SyncedTasks",
                columns: new[] { "DeveloperId", "AccountId", "TaskId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeveloperKpiSnapshots");

            migrationBuilder.DropTable(
                name: "SyncedTasks");

            migrationBuilder.DropTable(
                name: "KpiSyncRuns");
        }
    }
}
