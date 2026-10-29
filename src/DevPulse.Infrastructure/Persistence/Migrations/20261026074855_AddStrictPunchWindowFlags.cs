using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStrictPunchWindowFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "StrictPunchIn",
                table: "AttendanceSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "StrictPunchOut",
                table: "AttendanceSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StrictPunchIn",
                table: "AttendanceSettings");

            migrationBuilder.DropColumn(
                name: "StrictPunchOut",
                table: "AttendanceSettings");
        }
    }
}
