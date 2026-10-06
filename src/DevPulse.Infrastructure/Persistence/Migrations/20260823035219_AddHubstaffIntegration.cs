using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHubstaffIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HubstaffOrganizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    HubstaffOrganizationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EncryptedPersonalAccessToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncedToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastValidatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastValidationMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HubstaffOrganizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HubstaffSyncRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ActivitiesFetched = table.Column<int>(type: "int", nullable: false),
                    ActivitiesUpserted = table.Column<int>(type: "int", nullable: false),
                    UnmappedUsersSkipped = table.Column<int>(type: "int", nullable: false),
                    OrganizationCount = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TriggeredManually = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HubstaffSyncRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeveloperHubstaffMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeveloperId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HubstaffOrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HubstaffUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeveloperHubstaffMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeveloperHubstaffMappings_Developers_DeveloperId",
                        column: x => x.DeveloperId,
                        principalTable: "Developers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeveloperHubstaffMappings_HubstaffOrganizations_HubstaffOrganizationId",
                        column: x => x.HubstaffOrganizationId,
                        principalTable: "HubstaffOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HubstaffDailyActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HubstaffOrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HubstaffDailyActivityId = table.Column<long>(type: "bigint", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    HubstaffUserId = table.Column<int>(type: "int", nullable: false),
                    DeveloperId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    HubstaffUserEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    TrackedSeconds = table.Column<int>(type: "int", nullable: false),
                    BillableSeconds = table.Column<int>(type: "int", nullable: false),
                    IdleSeconds = table.Column<int>(type: "int", nullable: false),
                    ManualSeconds = table.Column<int>(type: "int", nullable: false),
                    InputTrackedSeconds = table.Column<int>(type: "int", nullable: false),
                    OverallActiveSeconds = table.Column<int>(type: "int", nullable: false),
                    HubstaffUpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HubstaffDailyActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HubstaffDailyActivities_Developers_DeveloperId",
                        column: x => x.DeveloperId,
                        principalTable: "Developers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HubstaffDailyActivities_HubstaffOrganizations_HubstaffOrganizationId",
                        column: x => x.HubstaffOrganizationId,
                        principalTable: "HubstaffOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperHubstaffMappings_DeveloperId",
                table: "DeveloperHubstaffMappings",
                column: "DeveloperId");

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperHubstaffMappings_HubstaffOrganizationId_DeveloperId",
                table: "DeveloperHubstaffMappings",
                columns: new[] { "HubstaffOrganizationId", "DeveloperId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperHubstaffMappings_HubstaffOrganizationId_HubstaffUserId",
                table: "DeveloperHubstaffMappings",
                columns: new[] { "HubstaffOrganizationId", "HubstaffUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HubstaffDailyActivities_DeveloperId_WorkDate",
                table: "HubstaffDailyActivities",
                columns: new[] { "DeveloperId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HubstaffDailyActivities_HubstaffOrganizationId_HubstaffDailyActivityId",
                table: "HubstaffDailyActivities",
                columns: new[] { "HubstaffOrganizationId", "HubstaffDailyActivityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HubstaffDailyActivities_HubstaffOrganizationId_HubstaffUserId_WorkDate",
                table: "HubstaffDailyActivities",
                columns: new[] { "HubstaffOrganizationId", "HubstaffUserId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HubstaffDailyActivities_HubstaffOrganizationId_WorkDate",
                table: "HubstaffDailyActivities",
                columns: new[] { "HubstaffOrganizationId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HubstaffOrganizations_OrganizationId",
                table: "HubstaffOrganizations",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HubstaffSyncRuns_StartedAtUtc",
                table: "HubstaffSyncRuns",
                column: "StartedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeveloperHubstaffMappings");

            migrationBuilder.DropTable(
                name: "HubstaffDailyActivities");

            migrationBuilder.DropTable(
                name: "HubstaffSyncRuns");

            migrationBuilder.DropTable(
                name: "HubstaffOrganizations");
        }
    }
}
