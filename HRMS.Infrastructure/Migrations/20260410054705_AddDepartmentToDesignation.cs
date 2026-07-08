using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentToDesignation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure the column is nullable (it may have been created as NOT NULL by a failed prior attempt)
            migrationBuilder.AlterColumn<int>(
                name: "DepartmentId",
                table: "Designations",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: false,
                oldDefaultValue: 0);

            // Nullify any rows with DepartmentId=0 (invalid FK value)
            migrationBuilder.Sql("UPDATE `Designations` SET `DepartmentId` = NULL WHERE `DepartmentId` = 0;");

            migrationBuilder.AddForeignKey(
                name: "FK_Designations_Departments_DepartmentId",
                table: "Designations",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Designations_Departments_DepartmentId",
                table: "Designations");

            migrationBuilder.DropIndex(
                name: "IX_Designations_DepartmentId",
                table: "Designations");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Designations");
        }
    }
}
