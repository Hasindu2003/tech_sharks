using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSeparationModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop tables if they exist to start fresh for separation modules
            migrationBuilder.Sql("DROP TABLE IF EXISTS DeathDocuments;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS DeathRequests;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS ResignationDocuments;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS ResignationRequests;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS TerminationDocuments;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS TerminationRequests;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS TransferRequests;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS TransferApprovals;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS EmployeeTransfers;");

            // Helper to add columns safely in MySQL
            void AddColumnSafely(string table, string column, string definition)
            {
                var safeDefinition = definition.Replace("'", "''");
                migrationBuilder.Sql($@"
                    SET @dbname = DATABASE();
                    SET @tablename = '{table}';
                    SET @columnname = '{column}';
                    SET @preparedStatement = (SELECT IF(
                      (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                       WHERE TABLE_SCHEMA = @dbname
                         AND TABLE_NAME = @tablename
                         AND COLUMN_NAME = @columnname) > 0,
                      'SELECT 1',
                      'ALTER TABLE `{table}` ADD `{column}` {safeDefinition}'
                    ));
                    PREPARE stmt FROM @preparedStatement;
                    EXECUTE stmt;
                    DEALLOCATE PREPARE stmt;
                ");
            }

            // Safe column additions for Notifications
            AddColumnSafely("Notifications", "TransferRequestId", "INT NULL");
            AddColumnSafely("Notifications", "Type", "INT NULL");

            // Safe alter column for UserId in Notifications
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'Notifications';
                SET @columnname = 'UserId';
                SET @preparedStatement = (SELECT IF(
                  (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_SCHEMA = @dbname
                     AND TABLE_NAME = @tablename
                     AND COLUMN_NAME = @columnname
                     AND COLUMN_TYPE = 'varchar(255)') > 0,
                  'SELECT 1',
                  'ALTER TABLE `Notifications` MODIFY `UserId` VARCHAR(255) NOT NULL'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Safe column additions for AspNetUsers
            AddColumnSafely("AspNetUsers", "Branch", "VARCHAR(200) NOT NULL DEFAULT ''");
            AddColumnSafely("AspNetUsers", "DateOfJoining", "DATETIME(6) NOT NULL DEFAULT '0001-01-01 00:00:00'");
            AddColumnSafely("AspNetUsers", "Department", "VARCHAR(100) NULL");
            AddColumnSafely("AspNetUsers", "Designation", "VARCHAR(200) NOT NULL DEFAULT ''");
            AddColumnSafely("AspNetUsers", "EpfNumber", "VARCHAR(20) NOT NULL DEFAULT ''");
            AddColumnSafely("AspNetUsers", "FullName", "VARCHAR(100) NOT NULL DEFAULT ''");

            // Comments out the automatic AddColumn calls below as they are now handled by AddColumnSafely
            /*
            migrationBuilder.AddColumn<int>(
                name: "TransferRequestId",
                table: "Notifications",
                type: "int",
                nullable: true);
            ...
            */

            migrationBuilder.CreateTable(
                name: "DeathRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EmployeeName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EpfNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmployeeEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Branch = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Department = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Designation = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DateOfDeath = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    NatureOfDeath = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NomineeName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NomineeRelation = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NomineeContact = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AdditionalRemarks = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HasOutstandingLoans = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsLoanGuarantor = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ObligationDetails = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InitiatedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    BMReview = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BMReviewDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    BMComments = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BMEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AMReview = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AMReviewDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AMComments = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AMEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HRReview = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HRReviewDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    HRComments = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HREmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AccountDeactivated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AccountDeactivatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AccountDeactivatedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PayrollStopped = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FinanceClearanceTriggered = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeathRequests", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ResignationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EmployeeName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EpfNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmployeeEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Branch = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Department = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Designation = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReasonForResignation = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResignationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    NoticePeriodDays = table.Column<int>(type: "int", nullable: false),
                    AdditionalRemarks = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HasOutstandingLoans = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsLoanGuarantor = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    HasOverridePermission = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ObligationDetails = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InitiatedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    BMReview = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BMReviewDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    BMComments = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BMEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AMReview = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AMReviewDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AMComments = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AMEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HRReview = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HRReviewDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    HRComments = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HREmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AcceptanceLetterGenerated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AcceptanceLetterDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AccountDeactivated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AccountDeactivatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AccountDeactivatedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResignationRequests", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TerminationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EmployeeName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EpfNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmployeeEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Branch = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Department = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Designation = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TerminationType = table.Column<int>(type: "int", nullable: false),
                    ReasonForTermination = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InitiationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EffectiveTerminationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SupervisorRemarks = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SpecialRemarks = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DirectObligations = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IndirectObligations = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HasOutstandingLoans = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsLoanGuarantor = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    HasOverridePermission = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InitiatedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InitiatedByRole = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ApproverReview = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApproverReviewDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ApproverComments = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApprovedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FinanceClearanceCompleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FinanceClearanceDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FinanceClearanceNotes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerminationRequests", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TransferRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EmployeeName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EpfNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmployeeEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CurrentBranch = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CurrentDesignation = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Department = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedBranch = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PreferredDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    YearsOfService = table.Column<int>(type: "int", nullable: false),
                    RequestedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedByRole = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DocumentData = table.Column<byte[]>(type: "longblob", nullable: true),
                    DocumentFileName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DocumentContentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HRManagerReview = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HRManagerReviewDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    HRManagerComments = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CurrentBMReview = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CurrentBMReviewDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CurrentBMComments = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetBMReview = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetBMReviewDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TargetBMComments = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AreaManagerReview = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AreaManagerReviewDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AreaManagerComments = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferRequests", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DeathDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DeathRequestId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Content = table.Column<byte[]>(type: "longblob", nullable: false),
                    DocumentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UploadedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeathDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeathDocuments_DeathRequests_DeathRequestId",
                        column: x => x.DeathRequestId,
                        principalTable: "DeathRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ResignationDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ResignationRequestId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DocumentData = table.Column<byte[]>(type: "longblob", nullable: false),
                    UploadedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResignationDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResignationDocuments_ResignationRequests_ResignationRequestId",
                        column: x => x.ResignationRequestId,
                        principalTable: "ResignationRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TerminationDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TerminationRequestId = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DocumentData = table.Column<byte[]>(type: "longblob", nullable: false),
                    UploadedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerminationDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TerminationDocuments_TerminationRequests_TerminationRequestId",
                        column: x => x.TerminationRequestId,
                        principalTable: "TerminationRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedAt",
                table: "Notifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_DeathDocuments_DeathRequestId",
                table: "DeathDocuments",
                column: "DeathRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_DeathRequests_CreatedDate",
                table: "DeathRequests",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_DeathRequests_EpfNumber",
                table: "DeathRequests",
                column: "EpfNumber");

            migrationBuilder.CreateIndex(
                name: "IX_DeathRequests_InitiatedBy",
                table: "DeathRequests",
                column: "InitiatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DeathRequests_Status",
                table: "DeathRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ResignationDocuments_ResignationRequestId",
                table: "ResignationDocuments",
                column: "ResignationRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ResignationRequests_CreatedDate",
                table: "ResignationRequests",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ResignationRequests_EpfNumber",
                table: "ResignationRequests",
                column: "EpfNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ResignationRequests_InitiatedBy",
                table: "ResignationRequests",
                column: "InitiatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ResignationRequests_Status",
                table: "ResignationRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TerminationDocuments_TerminationRequestId",
                table: "TerminationDocuments",
                column: "TerminationRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_TerminationRequests_CreatedDate",
                table: "TerminationRequests",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_TerminationRequests_EpfNumber",
                table: "TerminationRequests",
                column: "EpfNumber");

            migrationBuilder.CreateIndex(
                name: "IX_TerminationRequests_InitiatedBy",
                table: "TerminationRequests",
                column: "InitiatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TerminationRequests_Status",
                table: "TerminationRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TransferRequests_EpfNumber",
                table: "TransferRequests",
                column: "EpfNumber");

            migrationBuilder.CreateIndex(
                name: "IX_TransferRequests_RequestedBy",
                table: "TransferRequests",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TransferRequests_RequestedDate",
                table: "TransferRequests",
                column: "RequestedDate");

            migrationBuilder.CreateIndex(
                name: "IX_TransferRequests_Status",
                table: "TransferRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeathDocuments");

            migrationBuilder.DropTable(
                name: "ResignationDocuments");

            migrationBuilder.DropTable(
                name: "TerminationDocuments");

            migrationBuilder.DropTable(
                name: "TransferRequests");

            migrationBuilder.DropTable(
                name: "DeathRequests");

            migrationBuilder.DropTable(
                name: "ResignationRequests");

            migrationBuilder.DropTable(
                name: "TerminationRequests");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TransferRequestId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Branch",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DateOfJoining",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Designation",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EpfNumber",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Notifications",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
