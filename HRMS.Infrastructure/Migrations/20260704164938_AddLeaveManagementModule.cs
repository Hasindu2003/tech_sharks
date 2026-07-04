using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HRMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveManagementModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeaveApprovals_Employees_ApproverId",
                table: "LeaveApprovals");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveApprovals_Employees_EmployeeId",
                table: "LeaveApprovals");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveEntitlements_Employees_EmployeeId",
                table: "LeaveEntitlements");

            migrationBuilder.DropIndex(
                name: "IX_LeaveEntitlements_EmployeeId",
                table: "LeaveEntitlements");

            migrationBuilder.DropIndex(
                name: "IX_LeaveApprovals_EmployeeId",
                table: "LeaveApprovals");

            migrationBuilder.DropColumn(
                name: "RemainingDays",
                table: "LeaveEntitlements");

            migrationBuilder.DropColumn(
                name: "TotalDays",
                table: "LeaveEntitlements");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "LeaveApprovals");

            migrationBuilder.RenameColumn(
                name: "EmployeeId",
                table: "LeaveApprovals",
                newName: "Stage");

            migrationBuilder.RenameColumn(
                name: "ApproverId",
                table: "LeaveApprovals",
                newName: "ActorEmployeeId");

            migrationBuilder.RenameColumn(
                name: "ApprovalDate",
                table: "LeaveApprovals",
                newName: "ActionDate");

            migrationBuilder.RenameIndex(
                name: "IX_LeaveApprovals_ApproverId",
                table: "LeaveApprovals",
                newName: "IX_LeaveApprovals_ActorEmployeeId");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Leaves",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "LeaveType",
                table: "Leaves",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "AppliedAt",
                table: "Leaves",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "AttachmentPath",
                table: "Leaves",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Leaves",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Leaves",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DaysCount",
                table: "Leaves",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsHalfDay",
                table: "Leaves",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<decimal>(
                name: "UsedDays",
                table: "LeaveEntitlements",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "LeaveType",
                table: "LeaveEntitlements",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "AllocatedDays",
                table: "LeaveEntitlements",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CarriedForwardDays",
                table: "LeaveEntitlements",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Action",
                table: "LeaveApprovals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ManagerId",
                table: "Employees",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Holidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsRecurringYearly = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LeaveBalanceAdjustments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LeaveEntitlementId = table.Column<int>(type: "int", nullable: false),
                    DeltaDays = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Reason = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AdjustedByEmployeeId = table.Column<int>(type: "int", nullable: false),
                    AdjustedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveBalanceAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveBalanceAdjustments_Employees_AdjustedByEmployeeId",
                        column: x => x.AdjustedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveBalanceAdjustments_LeaveEntitlements_LeaveEntitlementId",
                        column: x => x.LeaveEntitlementId,
                        principalTable: "LeaveEntitlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LeavePolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LeaveType = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DaysPerYear = table.Column<int>(type: "int", nullable: true),
                    IsPaid = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AffectsBalance = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RequiresAttachment = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AllowHalfDay = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ExcludeWeekends = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ExcludeHolidays = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AllowPastDates = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CarryForwardAllowed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MaxCarryForwardDays = table.Column<int>(type: "int", nullable: true),
                    Active = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeavePolicies", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Message = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Link = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsRead = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "LeavePolicies",
                columns: new[] { "Id", "Active", "AffectsBalance", "AllowHalfDay", "AllowPastDates", "CarryForwardAllowed", "DaysPerYear", "ExcludeHolidays", "ExcludeWeekends", "IsPaid", "LeaveType", "MaxCarryForwardDays", "Name", "RequiresAttachment" },
                values: new object[,]
                {
                    { 1, true, true, true, false, true, 14, true, true, true, 0, 7, "Annual Leave", false },
                    { 2, true, true, true, false, false, 7, true, true, true, 1, null, "Casual Leave", false },
                    { 3, true, true, true, true, false, 14, true, true, true, 2, null, "Sick Leave", true },
                    { 4, true, true, false, true, false, 84, false, false, true, 3, null, "Maternity Leave", true },
                    { 5, true, false, false, false, false, null, true, true, true, 4, null, "Overseas Leave", false },
                    { 6, true, false, true, false, false, null, true, true, false, 5, null, "No Pay Leave", false }
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveEntitlements_EmployeeId_LeaveType_Year",
                table: "LeaveEntitlements",
                columns: new[] { "EmployeeId", "LeaveType", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ManagerId",
                table: "Employees",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalanceAdjustments_AdjustedByEmployeeId",
                table: "LeaveBalanceAdjustments",
                column: "AdjustedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalanceAdjustments_LeaveEntitlementId",
                table: "LeaveBalanceAdjustments",
                column: "LeaveEntitlementId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_EmployeeId",
                table: "Notifications",
                column: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Employees_ManagerId",
                table: "Employees",
                column: "ManagerId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveApprovals_Employees_ActorEmployeeId",
                table: "LeaveApprovals",
                column: "ActorEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveEntitlements_Employees_EmployeeId",
                table: "LeaveEntitlements",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Employees_ManagerId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveApprovals_Employees_ActorEmployeeId",
                table: "LeaveApprovals");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveEntitlements_Employees_EmployeeId",
                table: "LeaveEntitlements");

            migrationBuilder.DropTable(
                name: "Holidays");

            migrationBuilder.DropTable(
                name: "LeaveBalanceAdjustments");

            migrationBuilder.DropTable(
                name: "LeavePolicies");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_LeaveEntitlements_EmployeeId_LeaveType_Year",
                table: "LeaveEntitlements");

            migrationBuilder.DropIndex(
                name: "IX_Employees_ManagerId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "AppliedAt",
                table: "Leaves");

            migrationBuilder.DropColumn(
                name: "AttachmentPath",
                table: "Leaves");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Leaves");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Leaves");

            migrationBuilder.DropColumn(
                name: "DaysCount",
                table: "Leaves");

            migrationBuilder.DropColumn(
                name: "IsHalfDay",
                table: "Leaves");

            migrationBuilder.DropColumn(
                name: "AllocatedDays",
                table: "LeaveEntitlements");

            migrationBuilder.DropColumn(
                name: "CarriedForwardDays",
                table: "LeaveEntitlements");

            migrationBuilder.DropColumn(
                name: "Action",
                table: "LeaveApprovals");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                table: "Employees");

            migrationBuilder.RenameColumn(
                name: "Stage",
                table: "LeaveApprovals",
                newName: "EmployeeId");

            migrationBuilder.RenameColumn(
                name: "ActorEmployeeId",
                table: "LeaveApprovals",
                newName: "ApproverId");

            migrationBuilder.RenameColumn(
                name: "ActionDate",
                table: "LeaveApprovals",
                newName: "ApprovalDate");

            migrationBuilder.RenameIndex(
                name: "IX_LeaveApprovals_ActorEmployeeId",
                table: "LeaveApprovals",
                newName: "IX_LeaveApprovals_ApproverId");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Leaves",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "LeaveType",
                table: "Leaves",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "UsedDays",
                table: "LeaveEntitlements",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AlterColumn<string>(
                name: "LeaveType",
                table: "LeaveEntitlements",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "RemainingDays",
                table: "LeaveEntitlements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalDays",
                table: "LeaveEntitlements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "LeaveApprovals",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveEntitlements_EmployeeId",
                table: "LeaveEntitlements",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveApprovals_EmployeeId",
                table: "LeaveApprovals",
                column: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveApprovals_Employees_ApproverId",
                table: "LeaveApprovals",
                column: "ApproverId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveApprovals_Employees_EmployeeId",
                table: "LeaveApprovals",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveEntitlements_Employees_EmployeeId",
                table: "LeaveEntitlements",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
