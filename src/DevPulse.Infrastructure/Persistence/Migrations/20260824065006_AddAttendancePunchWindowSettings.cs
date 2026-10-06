using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendancePunchWindowSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PunchInAllowMinutesBeforeWorkStart",
                table: "AttendanceSettings",
                type: "int",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<int>(
                name: "PunchOutAllowMinutesAfterWorkEnd",
                table: "AttendanceSettings",
                type: "int",
                nullable: false,
                defaultValue: 120);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PunchInAllowMinutesBeforeWorkStart",
                table: "AttendanceSettings");

            migrationBuilder.DropColumn(
                name: "PunchOutAllowMinutesAfterWorkEnd",
                table: "AttendanceSettings");
        }
    }
}
