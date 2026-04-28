using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTransferWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BranchManagerReviewDate",
                table: "TransferRequests",
                newName: "TargetBMReviewDate");

            migrationBuilder.RenameColumn(
                name: "BranchManagerReview",
                table: "TransferRequests",
                newName: "TargetBMReview");

            migrationBuilder.RenameColumn(
                name: "BranchManagerComments",
                table: "TransferRequests",
                newName: "TargetBMComments");

            migrationBuilder.AddColumn<string>(
                name: "CurrentBMComments",
                table: "TransferRequests",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CurrentBMReview",
                table: "TransferRequests",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentBMReviewDate",
                table: "TransferRequests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "TransferRequests",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "HRManagerComments",
                table: "TransferRequests",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "HRManagerReview",
                table: "TransferRequests",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "HRManagerReviewDate",
                table: "TransferRequests",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentBMComments",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "CurrentBMReview",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "CurrentBMReviewDate",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "HRManagerComments",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "HRManagerReview",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "HRManagerReviewDate",
                table: "TransferRequests");

            migrationBuilder.RenameColumn(
                name: "TargetBMReviewDate",
                table: "TransferRequests",
                newName: "BranchManagerReviewDate");

            migrationBuilder.RenameColumn(
                name: "TargetBMReview",
                table: "TransferRequests",
                newName: "BranchManagerReview");

            migrationBuilder.RenameColumn(
                name: "TargetBMComments",
                table: "TransferRequests",
                newName: "BranchManagerComments");
        }
    }
}
