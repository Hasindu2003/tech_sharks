using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendancePunchFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckInSource",
                table: "Attendances",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CheckOutSource",
                table: "Attendances",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "EarlyLeaveMinutes",
                table: "Attendances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LateMinutes",
                table: "Attendances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OvertimeMinutes",
                table: "Attendances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkingMinutes",
                table: "Attendances",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckInSource",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "CheckOutSource",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "EarlyLeaveMinutes",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "LateMinutes",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "OvertimeMinutes",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "WorkingMinutes",
                table: "Attendances");
        }
    }
}
