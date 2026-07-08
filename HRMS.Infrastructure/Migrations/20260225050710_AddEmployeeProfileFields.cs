using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateConfirmed",
                table: "Employees",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ETFNumber",
                table: "Employees",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EmployeeType",
                table: "Employees",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Initials",
                table: "Employees",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "PreviousExperienceYears",
                table: "Employees",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ProbationPeriodMonths",
                table: "Employees",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReportingOfficerId",
                table: "Employees",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResidentialAddress",
                table: "Employees",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Sex",
                table: "Employees",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SpouseContactNo",
                table: "Employees",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SpouseName",
                table: "Employees",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ReportingOfficerId",
                table: "Employees",
                column: "ReportingOfficerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Employees_ReportingOfficerId",
                table: "Employees",
                column: "ReportingOfficerId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Employees_ReportingOfficerId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_ReportingOfficerId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "DateConfirmed",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ETFNumber",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "EmployeeType",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Initials",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PreviousExperienceYears",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ProbationPeriodMonths",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ReportingOfficerId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ResidentialAddress",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Sex",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "SpouseContactNo",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "SpouseName",
                table: "Employees");
        }
    }
}
