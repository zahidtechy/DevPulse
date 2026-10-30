using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceRecordCorrectedTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PunchInCorrectedAtUtc",
                table: "AttendanceRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PunchOutCorrectedAtUtc",
                table: "AttendanceRecords",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PunchInCorrectedAtUtc",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "PunchOutCorrectedAtUtc",
                table: "AttendanceRecords");
        }
    }
}
