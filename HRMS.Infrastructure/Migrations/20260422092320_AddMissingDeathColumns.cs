using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingDeathColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop old columns (present after AddDeathProcessModule migration)
            migrationBuilder.Sql("ALTER TABLE `DeathRequests` DROP COLUMN `FinancePaymentsReleased`");
            migrationBuilder.Sql("ALTER TABLE `DeathRequests` DROP COLUMN `FinancePaymentsReleasedDate`");
            migrationBuilder.Sql("ALTER TABLE `DeathRequests` DROP COLUMN `InitiatedByRole`");
            migrationBuilder.Sql("ALTER TABLE `DeathRequests` DROP COLUMN `NomineeAddress`");
            migrationBuilder.Sql("ALTER TABLE `DeathRequests` DROP COLUMN `NomineeNotified`");
            migrationBuilder.Sql("ALTER TABLE `DeathRequests` DROP COLUMN `NomineeNotifiedDate`");
            migrationBuilder.Sql("ALTER TABLE `DeathRequests` DROP COLUMN `NomineeRelationship`");
            migrationBuilder.Sql("ALTER TABLE `DeathRequests` DROP COLUMN `OfficialDocumentsDate`");
            migrationBuilder.Sql("ALTER TABLE `DeathRequests` DROP COLUMN `PayrollStoppedDate`");
            migrationBuilder.Sql("ALTER TABLE `DeathDocuments` DROP COLUMN `FilePath`");
            migrationBuilder.Sql("ALTER TABLE `DeathDocuments` DROP COLUMN `UploadedBy`");

            // Replace OfficialDocumentsGenerated with FinanceClearanceTriggered
            migrationBuilder.Sql("ALTER TABLE `DeathRequests` DROP COLUMN `OfficialDocumentsGenerated`");
            migrationBuilder.Sql(@"ALTER TABLE `DeathRequests`
                ADD COLUMN `FinanceClearanceTriggered` tinyint(1) NOT NULL DEFAULT 0");

            // Add new columns
            migrationBuilder.Sql(@"ALTER TABLE `DeathRequests`
                ADD COLUMN `AccountDeactivatedBy` varchar(256) CHARACTER SET utf8mb4 NULL");

            migrationBuilder.Sql(@"ALTER TABLE `DeathRequests`
                ADD COLUMN `AdditionalRemarks` varchar(1000) CHARACTER SET utf8mb4 NULL");

            migrationBuilder.Sql(@"ALTER TABLE `DeathRequests`
                ADD COLUMN `NomineeRelation` varchar(100) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''");

            migrationBuilder.Sql(@"ALTER TABLE `DeathDocuments`
                ADD COLUMN `Content` longblob NOT NULL DEFAULT ('')");

            // Column size adjustments
            migrationBuilder.Sql(@"ALTER TABLE `DeathRequests`
                MODIFY COLUMN `NomineeContact` varchar(100) CHARACTER SET utf8mb4 NOT NULL");

            migrationBuilder.Sql(@"ALTER TABLE `DeathRequests`
                MODIFY COLUMN `NatureOfDeath` varchar(500) CHARACTER SET utf8mb4 NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountDeactivatedBy",
                table: "DeathRequests");

            migrationBuilder.DropColumn(
                name: "AdditionalRemarks",
                table: "DeathRequests");

            migrationBuilder.DropColumn(
                name: "NomineeRelation",
                table: "DeathRequests");

            migrationBuilder.DropColumn(
                name: "Content",
                table: "DeathDocuments");

            migrationBuilder.RenameColumn(
                name: "FinanceClearanceTriggered",
                table: "DeathRequests",
                newName: "OfficialDocumentsGenerated");

            migrationBuilder.AlterColumn<string>(
                name: "NomineeContact",
                table: "DeathRequests",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "NatureOfDeath",
                table: "DeathRequests",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "FinancePaymentsReleased",
                table: "DeathRequests",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinancePaymentsReleasedDate",
                table: "DeathRequests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InitiatedByRole",
                table: "DeathRequests",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "NomineeAddress",
                table: "DeathRequests",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "NomineeNotified",
                table: "DeathRequests",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "NomineeNotifiedDate",
                table: "DeathRequests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NomineeRelationship",
                table: "DeathRequests",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "OfficialDocumentsDate",
                table: "DeathRequests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PayrollStoppedDate",
                table: "DeathRequests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "DeathDocuments",
                type: "varchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "UploadedBy",
                table: "DeathDocuments",
                type: "varchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
