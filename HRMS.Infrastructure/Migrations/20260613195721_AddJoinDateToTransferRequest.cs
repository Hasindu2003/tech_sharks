using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJoinDateToTransferRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "Branches");

            migrationBuilder.AddColumn<DateTime>(
                name: "JoinDate",
                table: "TransferRequests",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JoinDate",
                table: "TransferRequests");

            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "Departments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "Branches",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
