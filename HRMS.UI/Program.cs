using System.Globalization;
using HRMS.Domain.Common;
using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Welfare;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HRMS.Application.Services;
using HRMS.Application.Branches.Commands;
using HRMS.Application.Departments.Commands;
using HRMS.Application.Designations.Commands;
using HRMS.Application.Common;
using HRMS.Application.Entity.Commands;
using MySqlConnector;
using HRMS.UI.Services;
using HRMS.UI.Services.Impl;
using HRMS.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

// Set Default Culture and Timezone for Sri Lanka (SLST, UTC+05:30)
try
{
    var slCulture = new CultureInfo("en-LK");
    CultureInfo.DefaultThreadCurrentCulture = slCulture;
    CultureInfo.DefaultThreadCurrentUICulture = slCulture;
    Environment.SetEnvironmentVariable("TZ", "Asia/Colombo");
}
catch { }

var builder = WebApplication.CreateBuilder(args);

// Add DbContext
var connectionString = builder.Configuration["SQLCONNSTR_DefaultConnection"]
    ?? builder.Configuration["CUSTOMCONNSTR_DefaultConnection"]
    ?? builder.Configuration["ConnectionStrings__DefaultConnection"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["ConnectionStrings:DefaultConnection"]
    ?? builder.Configuration["MYSQLCONNSTR_DefaultConnection"]
    ?? string.Empty;

bool isSqlServer = connectionString.Contains("database.windows.net", StringComparison.OrdinalIgnoreCase) ||
                   connectionString.Contains("1433") ||
                   connectionString.Contains("Initial Catalog", StringComparison.OrdinalIgnoreCase) ||
                   connectionString.Contains("Trusted_Connection", StringComparison.OrdinalIgnoreCase);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (isSqlServer)
    {
        options.UseSqlServer(
            connectionString,
            sqlOptions => sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
        );
    }
    else
    {
        options.UseMySql(
            connectionString,
            new MySqlServerVersion(new Version(8, 0, 0)),
            mySqlOptions => mySqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
        );
    }
});

// Add ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password rules
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    // Use email as the unique identifier
    options.User.RequireUniqueEmail = true;

    // Email must be confirmed before login; phone/2FA not used
    options.SignIn.RequireConfirmedAccount = true;
    options.SignIn.RequireConfirmedEmail = true;
    options.SignIn.RequireConfirmedPhoneNumber = false;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure login/logout paths
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy =>
        policy.RequireRole("Admin"));
    options.AddPolicy("RequireAdminOrHR", policy =>
        policy.RequireRole("Admin", "HR Manager", "HR Officer"));
    options.AddPolicy("RequireManagers", policy =>
        policy.RequireRole("HR Manager", "HR Officer", "Area Manager", "Branch Manager", "Admin"));
    options.AddPolicy("RequireEmployee", policy =>
        policy.RequireRole("Employee"));
});

// Register command handlers (Settings / CQRS)
builder.Services.AddScoped<ICommandHandler<CreateBranchCommand, Result>, CreateBranchCommandHandler>();
builder.Services.AddScoped<ICommandHandler<EditBranchCommand, Result>, EditBranchCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CreateDepartmentCommand, Result>, CreateDepartmentCommandHandler>();
builder.Services.AddScoped<ICommandHandler<EditDepartmentCommand, Result>, EditDepartmentCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CreateDesignationCommand, Result>, CreateDesignationCommandHandler>();
builder.Services.AddScoped<ICommandHandler<EditDesignationCommand, Result>, EditDesignationCommandHandler>();

// Register services for Separation & Transfer
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ITrainingNotificationService, TrainingNotificationService>();
builder.Services.AddScoped<ITransferRequestService, TransferRequestService>();
builder.Services.AddScoped<ITerminationService, TerminationService>();
builder.Services.AddScoped<IResignationService, ResignationService>();
builder.Services.AddScoped<IDeathService, DeathService>();

// Register services for Attendance, Biometric Logs and Leaves (Imasha's part)
builder.Services.AddScoped<IBiometricLogService, BiometricLogService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<ILeaveService, LeaveService>();
builder.Services.AddScoped<IOverseasLeaveService, OverseasLeaveService>();
builder.Services.AddScoped<IMaternityLeaveService, MaternityLeaveService>();
builder.Services.AddScoped<ICVBankService, CVBankService>();

// Register background reminder hosted service for Calendar & Training
builder.Services.AddHostedService<HRMS.UI.Services.CalendarReminderBackgroundService>();

// Add Razor Pages with role-based folder authorization
builder.Services.AddScoped<HRMS.Infrastructure.Services.IEmailService, HRMS.Infrastructure.Services.EmailService>();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Settings", "RequireAdmin");
    options.Conventions.AuthorizeFolder("/Employees", "RequireManagers");
    options.Conventions.AuthorizeFolder("/Documents", "RequireAdminOrHR");
    options.Conventions.AuthorizeFolder("/Employee", "RequireEmployee");
});

var app = builder.Build();

// Ensure database exists, then apply migrations
try
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    var cs = context.Database.GetConnectionString();
    if (!isSqlServer && !string.IsNullOrEmpty(cs))
    {
        try
        {
            var csBuilder = new MySqlConnectionStringBuilder(cs);
            var databaseName = csBuilder.Database;

            csBuilder.Database = null;
            using (var connection = new MySqlConnection(csBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE IF NOT EXISTS `{databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
                await command.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Could not manually create database, continuing to EnsureCreated. Error: " + ex.Message);
        }
    }

    async Task EnsureTablesCreatedSafelyAsync(ApplicationDbContext ctx)
    {
        var dbCreator = ctx.Database.GetService<IDatabaseCreator>() as IRelationalDatabaseCreator;
        if (dbCreator != null && !await dbCreator.HasTablesAsync())
        {
            var ddlScript = ctx.Database.GenerateCreateScript();
            ddlScript = System.Text.RegularExpressions.Regex.Replace(ddlScript, @"\bON\s+DELETE\s+CASCADE\b", "ON DELETE NO ACTION", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var batches = ddlScript.Split(new[] { "\r\nGO\r\n", "\nGO\n", "\r\nGO", "\nGO" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var batch in batches)
            {
                var trimmed = batch.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    try
                    {
                        await ctx.Database.ExecuteSqlRawAsync(trimmed);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[DDL Execution] Statement note: {ex.Message}");
                    }
                }
            }
        }
    }

    await EnsureTablesCreatedSafelyAsync(context);

    if (isSqlServer)
    {
        try
        {
            var sql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BugReports')
                BEGIN
                    CREATE TABLE [dbo].[BugReports] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [Title] nvarchar(255) NOT NULL,
                        [Description] nvarchar(max) NOT NULL,
                        [Severity] nvarchar(50) NOT NULL DEFAULT 'Medium',
                        [Category] nvarchar(50) NOT NULL DEFAULT 'UI/UX',
                        [Status] nvarchar(50) NOT NULL DEFAULT 'Open',
                        [PageUrl] nvarchar(500) NOT NULL,
                        [ReportedByUsername] nvarchar(256) NULL,
                        [ReportedByRole] nvarchar(100) NULL,
                        [ReportedByBranch] nvarchar(200) NULL,
                        [UserAgent] nvarchar(500) NULL,
                        [ScreenResolution] nvarchar(50) NULL,
                        [ConsoleErrors] nvarchar(max) NULL,
                        [ScreenshotPath] nvarchar(500) NULL,
                        [DeveloperNotes] nvarchar(max) NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [ResolvedAt] datetime2 NULL
                    );
                END

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'JobOpenings')
                BEGIN
                    CREATE TABLE [dbo].[JobOpenings] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [JobCode] nvarchar(50) NOT NULL,
                        [Title] nvarchar(200) NOT NULL,
                        [Description] nvarchar(max) NULL,
                        [Requirements] nvarchar(max) NULL,
                        [DepartmentId] int NULL,
                        [BranchId] int NULL,
                        [EmploymentType] nvarchar(50) NOT NULL DEFAULT 'Full-Time',
                        [MinimumExperienceYears] int NOT NULL DEFAULT 0,
                        [MinimumEducationLevel] nvarchar(50) NOT NULL DEFAULT 'None',
                        [RequiredSkills] nvarchar(max) NULL,
                        [Status] nvarchar(50) NOT NULL DEFAULT 'Open',
                        [CreatedDate] datetime2 NOT NULL,
                        [ClosingDate] datetime2 NULL,
                        [CreatedByUserId] nvarchar(256) NULL
                    );
                END

                IF COL_LENGTH('dbo.CVBanks', 'JobOpeningId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[CVBanks] ADD [JobOpeningId] int NULL;
                END";
            await context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Schema Init SQL Server] {ex.Message}");
        }
    }

    if (!isSqlServer && !string.IsNullOrEmpty(cs))
    {
        // Repair columns that may be missing due to a partial migration failure or schema drift
        using (var connection = new MySqlConnection(cs))
        {
            await connection.OpenAsync();

        async Task AddColumnIfMissing(string table, string column, string definition)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM information_schema.COLUMNS " +
                              $"WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{column}'";
            if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 0)
            {
                var alter = connection.CreateCommand();
                alter.CommandText = $"ALTER TABLE `{table}` ADD COLUMN `{column}` {definition}";
                await alter.ExecuteNonQueryAsync();
            }
        }

        async Task ModifyColumnTypeIfExists(string table, string column, string definition)
        {
            try
            {
                var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM information_schema.COLUMNS " +
                                  $"WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{column}'";
                if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0)
                {
                    var alter = connection.CreateCommand();
                    alter.CommandText = $"ALTER TABLE `{table}` MODIFY COLUMN `{column}` {definition}";
                    await alter.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ModifyColumnTypeIfExists Warning] {table}.{column}: {ex.Message}");
            }
        }

        // Migrate Leave and LeaveEntitlement columns to double to prevent MySQL InvalidCastException
        await ModifyColumnTypeIfExists("LeaveEntitlements", "TotalDays", "double NOT NULL DEFAULT 0");
        await ModifyColumnTypeIfExists("LeaveEntitlements", "UsedDays", "double NOT NULL DEFAULT 0");
        await ModifyColumnTypeIfExists("LeaveEntitlements", "RemainingDays", "double NOT NULL DEFAULT 0");
        await ModifyColumnTypeIfExists("Leaves", "TotalDays", "double NOT NULL DEFAULT 0");

        // AspNetUsers custom profile and security columns
        await AddColumnIfMissing("AspNetUsers", "EmployeeId", "int NULL");
        await AddColumnIfMissing("AspNetUsers", "MustChangePassword", "tinyint(1) NOT NULL DEFAULT 1");
        await AddColumnIfMissing("AspNetUsers", "FullName", "longtext NULL");
        await AddColumnIfMissing("AspNetUsers", "Branch", "longtext NULL");
        await AddColumnIfMissing("AspNetUsers", "Department", "longtext NULL");
        await AddColumnIfMissing("AspNetUsers", "Designation", "longtext NULL");
        await AddColumnIfMissing("AspNetUsers", "DateOfJoining", "datetime(6) NULL");
        await AddColumnIfMissing("AspNetUsers", "EpfNumber", "longtext NULL");
        await AddColumnIfMissing("AspNetUsers", "ManagedBranches", "longtext NULL");

        // Employees & DraftEmployees BankName and Salary columns
        await AddColumnIfMissing("Employees", "BankName", "longtext NULL");
        await AddColumnIfMissing("DraftEmployees", "BankName", "longtext NULL");
        await AddColumnIfMissing("DraftEmployees", "BasicSalary", "decimal(18,2) NULL");

        // JoinDate added for service duration display
        await AddColumnIfMissing("TransferRequests", "JoinDate", "datetime(6) NULL");

        // DeptHead fields added for 5-stage transfer workflow
        await AddColumnIfMissing("TransferRequests", "DeptHeadReview", "varchar(50) NULL");
        await AddColumnIfMissing("TransferRequests", "DeptHeadReviewDate", "datetime(6) NULL");
        await AddColumnIfMissing("TransferRequests", "DeptHeadComments", "varchar(1000) NULL");

        // Leave fields added for Imasha's modules
        await AddColumnIfMissing("Leaves", "AppliedDate", "datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)");
        await AddColumnIfMissing("Leaves", "TotalDays", "double NOT NULL DEFAULT 0");
        await AddColumnIfMissing("Leaves", "IsHalfDay", "tinyint(1) NOT NULL DEFAULT 0");
        await AddColumnIfMissing("Leaves", "HalfDaySession", "varchar(50) NULL");
        await AddColumnIfMissing("Leaves", "AttachmentPath", "longtext NULL");
        await AddColumnIfMissing("Leaves", "RejectionReason", "longtext NULL");
        await AddColumnIfMissing("Leaves", "ApprovedById", "int NULL");
        await AddColumnIfMissing("Leaves", "ApprovedDate", "datetime(6) NULL");

        // MaternityLeave fields
        await AddColumnIfMissing("MaternityLeaves", "LeaveLevel", "int NOT NULL DEFAULT 1");
        await AddColumnIfMissing("MaternityLeaves", "ChildNumber", "int NOT NULL DEFAULT 1");
        await AddColumnIfMissing("MaternityLeaves", "MedicalCertificatePath", "longtext NULL");
        await AddColumnIfMissing("MaternityLeaves", "DoctorLetterPath", "longtext NULL");
        await AddColumnIfMissing("MaternityLeaves", "VerificationStatus", "varchar(50) NOT NULL DEFAULT 'Pending'");
        await AddColumnIfMissing("MaternityLeaves", "VerificationComments", "longtext NULL");

        // OverseasLeave fields
        await AddColumnIfMissing("OverseasLeaves", "ContactDetailsOverseas", "longtext NULL");
        await AddColumnIfMissing("OverseasLeaves", "PassportCopyPath", "longtext NULL");
        await AddColumnIfMissing("OverseasLeaves", "ConfirmationLetterPath", "longtext NULL");
        await AddColumnIfMissing("OverseasLeaves", "VerificationStatus", "varchar(50) NOT NULL DEFAULT 'New'");
        await AddColumnIfMissing("OverseasLeaves", "VerificationComments", "longtext NULL");
        await AddColumnIfMissing("OverseasLeaves", "BoardApprovalStatus", "varchar(50) NOT NULL DEFAULT 'Pending'");
        await AddColumnIfMissing("OverseasLeaves", "BoardRejectionReason", "longtext NULL");

        // MaternityPayments fields
        await AddColumnIfMissing("MaternityPayments", "SalaryAdjustmentType", "varchar(50) NOT NULL DEFAULT 'Full'");
        await AddColumnIfMissing("MaternityPayments", "NursingBreakConfig", "longtext NULL");
        // Termination multi-stage review fields
        await AddColumnIfMissing("TerminationRequests", "BMReview", "varchar(50) NULL");
        await AddColumnIfMissing("TerminationRequests", "BMReviewDate", "datetime(6) NULL");
        await AddColumnIfMissing("TerminationRequests", "BMComments", "varchar(1000) NULL");
        await AddColumnIfMissing("TerminationRequests", "BMEmail", "varchar(256) NULL");
        await AddColumnIfMissing("TerminationRequests", "AMReview", "varchar(50) NULL");
        await AddColumnIfMissing("TerminationRequests", "AMReviewDate", "datetime(6) NULL");
        await AddColumnIfMissing("TerminationRequests", "AMComments", "varchar(1000) NULL");
        await AddColumnIfMissing("TerminationRequests", "AMEmail", "varchar(256) NULL");
        await AddColumnIfMissing("TerminationRequests", "HRReview", "varchar(50) NULL");
        await AddColumnIfMissing("TerminationRequests", "HRReviewDate", "datetime(6) NULL");
        await AddColumnIfMissing("TerminationRequests", "HRComments", "varchar(1000) NULL");
        await AddColumnIfMissing("TerminationRequests", "HREmail", "varchar(256) NULL");

        // Modify PaymentDate to be nullable
        var alterDate = connection.CreateCommand();
        alterDate.CommandText = "ALTER TABLE `MaternityPayments` MODIFY COLUMN `PaymentDate` datetime(6) NULL";
        try { await alterDate.ExecuteNonQueryAsync(); } catch {}

        // Create LeaveAllocationSettings table if not exists
        var createTableCmd = connection.CreateCommand();
        createTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS `LeaveAllocationSettings` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `EmployeeType` varchar(50) NOT NULL DEFAULT 'Permanent',
                `LeaveType` varchar(50) NOT NULL,
                `DefaultDays` int NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
        try { await createTableCmd.ExecuteNonQueryAsync(); } catch {}

        await AddColumnIfMissing("LeaveAllocationSettings", "EmployeeType", "varchar(50) NOT NULL DEFAULT 'Permanent'");

        // Drop old LeaveType single-column unique index if present
        try
        {
            var dropIdxCmd = connection.CreateCommand();
            dropIdxCmd.CommandText = "ALTER TABLE `LeaveAllocationSettings` DROP INDEX `LeaveType`";
            await dropIdxCmd.ExecuteNonQueryAsync();
        }
        catch {}

        // Attendance fields
        await AddColumnIfMissing("Attendances", "TotalHours", "double NULL");

        // Create Imesha's module tables if not exists
        var createImeshaTablesCmd = connection.CreateCommand();
        createImeshaTablesCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS CVBanks (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                CandidateName LONGTEXT NOT NULL,
                Email LONGTEXT NOT NULL,
                ContactNumber LONGTEXT NULL,
                AppliedPosition LONGTEXT NOT NULL,
                ExperienceYears INT NOT NULL,
                Skills LONGTEXT NULL,
                CVFilePath LONGTEXT NULL,
                UploadedDate DATETIME(6) NOT NULL,
                HasDegree TINYINT(1) NOT NULL,
                HasMasters TINYINT(1) NOT NULL,
                ExperienceScore INT NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `JobOpenings` (
                `Id` INT AUTO_INCREMENT PRIMARY KEY,
                `JobCode` VARCHAR(50) NOT NULL,
                `Title` VARCHAR(200) NOT NULL,
                `Description` LONGTEXT NULL,
                `Requirements` LONGTEXT NULL,
                `DepartmentId` INT NULL,
                `BranchId` INT NULL,
                `EmploymentType` VARCHAR(50) NOT NULL DEFAULT 'Full-Time',
                `MinimumExperienceYears` INT NOT NULL DEFAULT 0,
                `MinimumEducationLevel` VARCHAR(50) NOT NULL DEFAULT 'None',
                `RequiredSkills` LONGTEXT NULL,
                `Status` VARCHAR(50) NOT NULL DEFAULT 'Open',
                `CreatedDate` DATETIME(6) NOT NULL,
                `ClosingDate` DATETIME(6) NULL,
                `CreatedByUserId` VARCHAR(256) NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS InternEvaluations (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                EmployeeId INT NOT NULL,
                EvaluatedBy INT NOT NULL,
                EvaluationMonth INT NOT NULL,
                TechnicalSkillsScore INT NOT NULL,
                CommunicationScore INT NOT NULL,
                TeamworkScore INT NOT NULL,
                Comments LONGTEXT NULL,
                EvaluationDate DATETIME(6) NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS ProbationEvaluations (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                EmployeeId INT NOT NULL,
                EvaluatedBy INT NOT NULL,
                EvaluationMonth INT NOT NULL,
                PerformanceScore INT NOT NULL,
                AttendanceScore INT NOT NULL,
                ConductScore INT NOT NULL,
                Comments LONGTEXT NULL,
                EvaluationDate DATETIME(6) NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        ";
        try { await createImeshaTablesCmd.ExecuteNonQueryAsync(); } catch (Exception ex) { Console.WriteLine("Error creating Imesha's tables: " + ex.Message); }

        await AddColumnIfMissing("CVBanks", "JobOpeningId", "INT NULL");

        // Clean legacy allowances from Payslips
        var cleanupPayslipsCmd = connection.CreateCommand();
        cleanupPayslipsCmd.CommandText = @"
            UPDATE `Payslips` 
            SET `HousingAllowance` = 0, 
                `TransportAllowance` = 0, 
                `MedicalAllowance` = 0, 
                `GrossPay` = `BasicSalary` + `Bonuses`, 
                `TotalDeductions` = `EpfEmployee` + `TaxDeduction`, 
                `NetPay` = (`BasicSalary` + `Bonuses`) - (`EpfEmployee` + `TaxDeduction`)
            WHERE `HousingAllowance` > 0 OR `TransportAllowance` > 0 OR `MedicalAllowance` > 0;
        ";
        try { await cleanupPayslipsCmd.ExecuteNonQueryAsync(); } catch {}

        // Create Separation & Transfer module tables if not exists
        var createSeparationTablesCmd = connection.CreateCommand();
        createSeparationTablesCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS `ResignationRequests` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `EmployeeName` varchar(100) NOT NULL,
                `EpfNumber` varchar(20) NOT NULL,
                `EmployeeEmail` varchar(256) NOT NULL,
                `Branch` varchar(200) NOT NULL,
                `Department` varchar(100) NULL,
                `Designation` varchar(200) NOT NULL,
                `ReasonForResignation` varchar(1000) NOT NULL,
                `ResignationDate` datetime(6) NOT NULL,
                `EffectiveDate` datetime(6) NOT NULL,
                `NoticePeriodDays` int NOT NULL DEFAULT 0,
                `AdditionalRemarks` varchar(1000) NULL,
                `HasOutstandingLoans` tinyint(1) NOT NULL DEFAULT 0,
                `IsLoanGuarantor` tinyint(1) NOT NULL DEFAULT 0,
                `HasOverridePermission` tinyint(1) NOT NULL DEFAULT 0,
                `ObligationDetails` varchar(2000) NULL,
                `Status` int NOT NULL DEFAULT 0,
                `InitiatedBy` varchar(256) NOT NULL,
                `CreatedDate` datetime(6) NOT NULL,
                `LastModifiedDate` datetime(6) NOT NULL,
                `BMReview` varchar(50) NULL,
                `BMReviewDate` datetime(6) NULL,
                `BMComments` varchar(1000) NULL,
                `BMEmail` varchar(256) NULL,
                `AMReview` varchar(50) NULL,
                `AMReviewDate` datetime(6) NULL,
                `AMComments` varchar(1000) NULL,
                `AMEmail` varchar(256) NULL,
                `HRReview` varchar(50) NULL,
                `HRReviewDate` datetime(6) NULL,
                `HRComments` varchar(1000) NULL,
                `HREmail` varchar(256) NULL,
                `AcceptanceLetterGenerated` tinyint(1) NOT NULL DEFAULT 0,
                `AcceptanceLetterDate` datetime(6) NULL,
                `AccountDeactivated` tinyint(1) NOT NULL DEFAULT 0,
                `AccountDeactivatedDate` datetime(6) NULL,
                `AccountDeactivatedBy` varchar(256) NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `ResignationDocuments` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `ResignationRequestId` int NOT NULL,
                `FileName` varchar(255) NOT NULL,
                `ContentType` varchar(100) NOT NULL,
                `Data` longblob NOT NULL,
                `UploadedDate` datetime(6) NOT NULL,
                INDEX `IX_ResignationDocuments_ResignationRequestId` (`ResignationRequestId`),
                CONSTRAINT `FK_ResignationDocuments_ResignationRequests` FOREIGN KEY (`ResignationRequestId`) REFERENCES `ResignationRequests` (`Id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `ResignationDepartmentReviews` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `ResignationRequestId` int NOT NULL,
                `DepartmentId` int NULL,
                `DepartmentName` varchar(100) NOT NULL,
                `ReviewerUserId` varchar(100) NULL,
                `ReviewerName` varchar(150) NULL,
                `ReviewerEmail` varchar(256) NULL,
                `Status` varchar(50) NOT NULL DEFAULT 'Pending',
                `Comments` varchar(1000) NULL,
                `ReviewDate` datetime(6) NULL,
                INDEX `IX_ResignationDeptReviews_ResignationRequestId` (`ResignationRequestId`),
                CONSTRAINT `FK_ResignationDeptReviews_ResignationRequests` FOREIGN KEY (`ResignationRequestId`) REFERENCES `ResignationRequests` (`Id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `TerminationRequests` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `EmployeeName` varchar(100) NOT NULL,
                `EpfNumber` varchar(20) NOT NULL,
                `EmployeeEmail` varchar(256) NOT NULL,
                `Branch` varchar(200) NOT NULL,
                `Department` varchar(100) NULL,
                `Designation` varchar(200) NOT NULL,
                `TerminationType` int NOT NULL DEFAULT 0,
                `ReasonForTermination` varchar(1000) NOT NULL,
                `InitiationDate` datetime(6) NOT NULL,
                `EffectiveTerminationDate` datetime(6) NOT NULL,
                `SupervisorRemarks` varchar(1000) NULL,
                `SpecialRemarks` varchar(1000) NULL,
                `DirectObligations` varchar(2000) NULL,
                `IndirectObligations` varchar(2000) NULL,
                `HasOutstandingLoans` tinyint(1) NOT NULL DEFAULT 0,
                `IsLoanGuarantor` tinyint(1) NOT NULL DEFAULT 0,
                `HasOverridePermission` tinyint(1) NOT NULL DEFAULT 0,
                `Status` int NOT NULL DEFAULT 0,
                `InitiatedBy` varchar(256) NOT NULL,
                `InitiatedByRole` varchar(50) NOT NULL,
                `CreatedDate` datetime(6) NOT NULL,
                `LastModifiedDate` datetime(6) NOT NULL,
                `ApproverReview` varchar(50) NULL,
                `ApproverReviewDate` datetime(6) NULL,
                `ApproverComments` varchar(1000) NULL,
                `ApprovedBy` varchar(256) NULL,
                `FinanceClearanceCompleted` tinyint(1) NOT NULL DEFAULT 0,
                `FinanceClearanceDate` datetime(6) NULL,
                `FinanceClearanceNotes` varchar(1000) NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `TerminationDocuments` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `TerminationRequestId` int NOT NULL,
                `FileName` varchar(255) NOT NULL,
                `ContentType` varchar(100) NOT NULL,
                `Data` longblob NOT NULL,
                `UploadedDate` datetime(6) NOT NULL,
                INDEX `IX_TerminationDocuments_TerminationRequestId` (`TerminationRequestId`),
                CONSTRAINT `FK_TerminationDocuments_TerminationRequests` FOREIGN KEY (`TerminationRequestId`) REFERENCES `TerminationRequests` (`Id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `TerminationDepartmentReviews` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `TerminationRequestId` int NOT NULL,
                `DepartmentName` varchar(100) NOT NULL,
                `ReviewerUserId` varchar(100) NULL,
                `ReviewerName` varchar(150) NULL,
                `ReviewerEmail` varchar(256) NULL,
                `Status` varchar(50) NOT NULL DEFAULT 'Pending',
                `Comments` varchar(1000) NULL,
                `ReviewDate` datetime(6) NULL,
                INDEX `IX_TerminationDeptReviews_TerminationRequestId` (`TerminationRequestId`),
                CONSTRAINT `FK_TerminationDeptReviews_TerminationRequests` FOREIGN KEY (`TerminationRequestId`) REFERENCES `TerminationRequests` (`Id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `DeathRequests` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `EmployeeName` varchar(100) NOT NULL,
                `EpfNumber` varchar(20) NOT NULL,
                `EmployeeEmail` varchar(256) NOT NULL,
                `Branch` varchar(200) NOT NULL,
                `Department` varchar(100) NULL,
                `Designation` varchar(200) NOT NULL,
                `DateOfDeath` datetime(6) NOT NULL,
                `NatureOfDeath` varchar(500) NOT NULL,
                `NomineeName` varchar(200) NOT NULL,
                `NomineeRelation` varchar(100) NOT NULL,
                `NomineeContact` varchar(100) NOT NULL,
                `AdditionalRemarks` varchar(1000) NULL,
                `HasOutstandingLoans` tinyint(1) NOT NULL DEFAULT 0,
                `IsLoanGuarantor` tinyint(1) NOT NULL DEFAULT 0,
                `ObligationDetails` varchar(2000) NULL,
                `Status` int NOT NULL DEFAULT 0,
                `InitiatedBy` varchar(256) NOT NULL,
                `CreatedDate` datetime(6) NOT NULL,
                `LastModifiedDate` datetime(6) NOT NULL,
                `BMReview` varchar(50) NULL,
                `BMReviewDate` datetime(6) NULL,
                `BMComments` varchar(1000) NULL,
                `BMEmail` varchar(256) NULL,
                `AMReview` varchar(50) NULL,
                `AMReviewDate` datetime(6) NULL,
                `AMComments` varchar(1000) NULL,
                `AMEmail` varchar(256) NULL,
                `HRReview` varchar(50) NULL,
                `HRReviewDate` datetime(6) NULL,
                `HRComments` varchar(1000) NULL,
                `HREmail` varchar(256) NULL,
                `AccountDeactivated` tinyint(1) NOT NULL DEFAULT 0,
                `AccountDeactivatedDate` datetime(6) NULL,
                `AccountDeactivatedBy` varchar(256) NULL,
                `PayrollStopped` tinyint(1) NOT NULL DEFAULT 0,
                `FinanceClearanceTriggered` tinyint(1) NOT NULL DEFAULT 0
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `DeathDocuments` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `DeathRequestId` int NOT NULL,
                `FileName` varchar(255) NOT NULL,
                `ContentType` varchar(100) NOT NULL,
                `Data` longblob NOT NULL,
                `UploadedDate` datetime(6) NOT NULL,
                INDEX `IX_DeathDocuments_DeathRequestId` (`DeathRequestId`),
                CONSTRAINT `FK_DeathDocuments_DeathRequests` FOREIGN KEY (`DeathRequestId`) REFERENCES `DeathRequests` (`Id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `TransferRequests` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `EmployeeName` varchar(100) NOT NULL,
                `EpfNumber` varchar(20) NOT NULL,
                `EmployeeEmail` varchar(256) NOT NULL,
                `CurrentBranch` varchar(200) NOT NULL,
                `CurrentDesignation` varchar(200) NOT NULL,
                `Department` varchar(100) NULL,
                `RequestedBranch` varchar(200) NOT NULL,
                `Reason` varchar(500) NOT NULL,
                `PreferredDate` datetime(6) NULL,
                `YearsOfService` int NOT NULL DEFAULT 0,
                `JoinDate` datetime(6) NULL,
                `RequestedBy` varchar(256) NOT NULL,
                `RequestedByRole` varchar(50) NOT NULL,
                `RequestedDate` datetime(6) NOT NULL,
                `Status` int NOT NULL DEFAULT 0,
                `DocumentData` longblob NULL,
                `DocumentFileName` varchar(256) NULL,
                `DocumentContentType` varchar(100) NULL,
                `DeptHeadReview` varchar(50) NULL,
                `DeptHeadReviewDate` datetime(6) NULL,
                `DeptHeadComments` varchar(1000) NULL,
                `CurrentBMReview` varchar(50) NULL,
                `CurrentBMReviewDate` datetime(6) NULL,
                `CurrentBMComments` varchar(1000) NULL,
                `TargetBMReview` varchar(50) NULL,
                `TargetBMReviewDate` datetime(6) NULL,
                `TargetBMComments` varchar(1000) NULL,
                `AreaManagerReview` varchar(50) NULL,
                `AreaManagerReviewDate` datetime(6) NULL,
                `AreaManagerComments` varchar(1000) NULL,
                `HRManagerReview` varchar(50) NULL,
                `HRManagerReviewDate` datetime(6) NULL,
                `HRManagerComments` varchar(1000) NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `Notifications` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `UserId` varchar(256) NOT NULL,
                `Title` varchar(200) NOT NULL,
                `Message` longtext NOT NULL,
                `TargetUrl` varchar(500) NOT NULL DEFAULT '',
                `IsRead` tinyint(1) NOT NULL DEFAULT 0,
                `CreatedAt` datetime(6) NOT NULL,
                `Type` int NULL,
                `TransferRequestId` int NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `DraftEmployees` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `FullName` longtext NULL,
                `Initials` longtext NULL,
                `Sex` longtext NULL,
                `EmployeeType` longtext NULL,
                `NIC` longtext NULL,
                `DateOfBirth` datetime(6) NULL,
                `DateJoined` datetime(6) NULL,
                `Email` longtext NULL,
                `PhoneNumber` longtext NULL,
                `ResidentialAddress` longtext NULL,
                `SpouseName` longtext NULL,
                `SpouseContactNo` longtext NULL,
                `EPFNumber` longtext NULL,
                `ETFNumber` longtext NULL,
                `BankAccountName` longtext NULL,
                `BankAccountNumber` longtext NULL,
                `DesignationId` int NULL,
                `DateConfirmed` datetime(6) NULL,
                `ProbationPeriodMonths` int NULL,
                `InternPeriodMonths` int NULL,
                `PreviousExperienceYears` decimal(18,2) NULL,
                `Status` longtext NULL,
                `LastUpdated` datetime(6) NULL,
                `DepartmentId` int NULL,
                `BranchId` int NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `EmployeeDocuments` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `EmployeeId` int NOT NULL,
                `DocumentType` varchar(100) NOT NULL,
                `FileName` varchar(255) NOT NULL,
                `StoredFileName` varchar(255) NOT NULL,
                `ContentType` varchar(100) NOT NULL,
                `UploadedAt` datetime(6) NOT NULL,
                `Status` varchar(50) NOT NULL DEFAULT 'Pending',
                `ReviewedAt` datetime(6) NULL,
                `ReviewedByUserId` varchar(256) NULL,
                `ReviewerNotes` varchar(1000) NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `BranchDepartments` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `BranchId` int NOT NULL,
                `DepartmentId` int NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `DepartmentDesignations` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `DepartmentId` int NOT NULL,
                `DesignationId` int NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `BugReports` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `Title` varchar(255) NOT NULL,
                `Description` longtext NOT NULL,
                `Severity` varchar(50) NOT NULL DEFAULT 'Medium',
                `Category` varchar(50) NOT NULL DEFAULT 'UI/UX',
                `Status` varchar(50) NOT NULL DEFAULT 'Open',
                `PageUrl` varchar(500) NOT NULL,
                `ReportedByUsername` varchar(256) NULL,
                `ReportedByRole` varchar(100) NULL,
                `ReportedByBranch` varchar(200) NULL,
                `UserAgent` varchar(500) NULL,
                `ScreenResolution` varchar(50) NULL,
                `ConsoleErrors` longtext NULL,
                `ScreenshotPath` varchar(500) NULL,
                `DeveloperNotes` longtext NULL,
                `CreatedAt` datetime(6) NOT NULL,
                `ResolvedAt` datetime(6) NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        ";
        try { await createSeparationTablesCmd.ExecuteNonQueryAsync(); } catch (Exception ex) { Console.WriteLine("Error creating Separation/BugReport tables: " + ex.Message); }

        // Add Trainer and Location columns to trainings table
        await AddColumnIfMissing("trainings", "Trainer", "longtext NULL");
        await AddColumnIfMissing("trainings", "Location", "longtext NULL");

        // Create Welfare and Payroll tables if not exists
        var createWelfareTablesCmd = connection.CreateCommand();
        createWelfareTablesCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS `welfaretype` (
                `welfare_type_id` int AUTO_INCREMENT PRIMARY KEY,
                `type_name` varchar(255) NOT NULL,
                `category` varchar(100) NULL,
                `max_eligible_amount` decimal(18,2) NOT NULL,
                `is_active` tinyint(1) NOT NULL DEFAULT 1,
                `created_at` datetime(6) NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `welfarerequest` (
                `request_id` int AUTO_INCREMENT PRIMARY KEY,
                `employee_id` int NOT NULL,
                `welfare_type_id` int NOT NULL,
                `request_date` datetime(6) NOT NULL,
                `requested_amount` decimal(18,2) NOT NULL,
                `approved_amount` decimal(18,2) NULL,
                `remark` longtext NULL,
                `status` varchar(50) NOT NULL,
                `is_draft` tinyint(1) NOT NULL,
                `created_at` datetime(6) NOT NULL,
                `submitted_by` int NOT NULL,
                `current_level` varchar(50) NULL,
                `current_status` varchar(50) NULL,
                FOREIGN KEY (`employee_id`) REFERENCES `Employees` (`Id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `welfareapproval` (
                `approval_id` int AUTO_INCREMENT PRIMARY KEY,
                `request_id` int NOT NULL,
                `approver_level` varchar(50) NOT NULL,
                `approver_id` int NOT NULL,
                `action` varchar(50) NOT NULL,
                `comments` longtext NULL,
                `action_date` datetime(6) NOT NULL,
                FOREIGN KEY (`request_id`) REFERENCES `welfarerequest` (`request_id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `welfaredocument` (
                `document_id` int AUTO_INCREMENT PRIMARY KEY,
                `request_id` int NOT NULL,
                `file_name` varchar(255) NOT NULL,
                `file_path` varchar(1000) NOT NULL,
                `file_type` varchar(100) NOT NULL,
                `uploaded_at` datetime(6) NOT NULL,
                FOREIGN KEY (`request_id`) REFERENCES `welfarerequest` (`request_id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `payrollsalary` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `EmployeeId` int NOT NULL,
                `BasicSalary` decimal(18,2) NOT NULL,
                `HousingAllowance` decimal(18,2) NOT NULL,
                `TransportAllowance` decimal(18,2) NOT NULL,
                `MedicalAllowance` decimal(18,2) NOT NULL,
                `EffectiveDate` datetime(6) NOT NULL,
                FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `payrollrun` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `Month` int NOT NULL,
                `Year` int NOT NULL,
                `BranchId` int NULL,
                `Status` varchar(50) NOT NULL,
                `ProcessedAt` datetime(6) NOT NULL,
                `TotalAmount` decimal(18,2) NOT NULL,
                `TotalEmployees` int NOT NULL,
                FOREIGN KEY (`BranchId`) REFERENCES `Branches` (`Id`) ON DELETE SET NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `payslip` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `PayrollRunId` int NOT NULL,
                `EmployeeId` int NOT NULL,
                `BasicSalary` decimal(18,2) NOT NULL,
                `HousingAllowance` decimal(18,2) NOT NULL,
                `TransportAllowance` decimal(18,2) NOT NULL,
                `MedicalAllowance` decimal(18,2) NOT NULL,
                `Bonuses` decimal(18,2) NOT NULL,
                `GrossPay` decimal(18,2) NOT NULL,
                `EpfEmployee` decimal(18,2) NOT NULL,
                `EpfEmployer` decimal(18,2) NOT NULL,
                `Etf` decimal(18,2) NOT NULL,
                `TaxDeduction` decimal(18,2) NOT NULL,
                `TotalDeductions` decimal(18,2) NOT NULL,
                `NetPay` decimal(18,2) NOT NULL,
                `Status` varchar(50) NOT NULL,
                FOREIGN KEY (`PayrollRunId`) REFERENCES `payrollrun` (`Id`) ON DELETE CASCADE,
                FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IF NOT EXISTS `payrollbonuses` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `EmployeeId` int NOT NULL,
                `BonusType` varchar(100) NOT NULL,
                `Amount` decimal(18,2) NOT NULL,
                `Month` int NOT NULL,
                `Year` int NOT NULL,
                `Reason` varchar(1000) NULL,
                FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        ";
        try { await createWelfareTablesCmd.ExecuteNonQueryAsync(); } catch (Exception ex) { Console.WriteLine("Error creating Welfare/Payroll tables: " + ex.Message); }

        await AddColumnIfMissing("payrollrun", "BranchId", "int NULL");

        // Create PayrollPolicySettings table if not exists
        var createPolicySettingsCmd = connection.CreateCommand();
        createPolicySettingsCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS `PayrollPolicySettings` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `BranchId` int NULL,
                `StandardMonthlyWorkingDays` int NOT NULL DEFAULT 21,
                `StandardDailyWorkingHours` decimal(5,2) NOT NULL DEFAULT 8.00,
                `StandardOtMultiplier` decimal(5,2) NOT NULL DEFAULT 1.50,
                `WeekendOtMultiplier` decimal(5,2) NOT NULL DEFAULT 2.00,
                `AutoCalculateOtOnPayroll` tinyint(1) NOT NULL DEFAULT 1,
                `LastModifiedDate` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
                `ModifiedBy` varchar(256) NULL,
                FOREIGN KEY (`BranchId`) REFERENCES `Branches` (`Id`) ON DELETE SET NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        ";
        try { await createPolicySettingsCmd.ExecuteNonQueryAsync(); } catch (Exception ex) { Console.WriteLine("Error creating PayrollPolicySettings table: " + ex.Message); }

        // Seed default global OT policy if none exists
        var checkPolicyCmd = connection.CreateCommand();
        checkPolicyCmd.CommandText = "SELECT COUNT(*) FROM `PayrollPolicySettings`";
        var policyCount = Convert.ToInt32(await checkPolicyCmd.ExecuteScalarAsync());
        if (policyCount == 0)
        {
            var seedPolicyCmd = connection.CreateCommand();
            seedPolicyCmd.CommandText = @"
                INSERT INTO `PayrollPolicySettings` (`BranchId`, `StandardMonthlyWorkingDays`, `StandardDailyWorkingHours`, `StandardOtMultiplier`, `WeekendOtMultiplier`, `AutoCalculateOtOnPayroll`, `LastModifiedDate`)
                VALUES (NULL, 21, 8.00, 1.50, 2.00, 1, NOW());
            ";
            try { await seedPolicyCmd.ExecuteNonQueryAsync(); } catch (Exception ex) { Console.WriteLine("Error seeding default OT policy: " + ex.Message); }
        }

        // Create CalendarEvents table if not exists
        var createCalendarEventsCmd = connection.CreateCommand();
        createCalendarEventsCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS `CalendarEvents` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `Title` varchar(255) NOT NULL,
                `Description` longtext NULL,
                `EventType` varchar(50) NOT NULL,
                `StartTime` datetime(6) NOT NULL,
                `EndTime` datetime(6) NOT NULL,
                `IsAllDay` tinyint(1) NOT NULL DEFAULT 0,
                `Location` varchar(255) NULL,
                `MeetingLink` varchar(500) NULL,
                `CreatedByUserId` varchar(256) NOT NULL,
                `EmployeeId` int NULL,
                `BranchId` int NULL,
                `DepartmentId` int NULL,
                `TrainingId` int NULL,
                `CreatedAt` datetime(6) NOT NULL,
                `DayBeforeNotificationSent` tinyint(1) NOT NULL DEFAULT 0,
                `HourBeforeNotificationSent` tinyint(1) NOT NULL DEFAULT 0
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        ";
        try { await createCalendarEventsCmd.ExecuteNonQueryAsync(); } catch (Exception ex) { Console.WriteLine("Error creating CalendarEvents table: " + ex.Message); }
        }
    }
}

// Seed roles, default branches, departments, and default users
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Seed roles
    string[] roles = ["Admin", "HR Manager", "Welfare Manager", "HR Officer", "Employee", "Area Manager", "Branch Manager", "Department Head"];
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    // Seed core managerial designations if not present
    string[] coreDesignations = ["Branch Manager", "Area Manager", "Department Head", "HR Manager", "Welfare Manager"];
    foreach (var desigTitle in coreDesignations)
    {
        if (!await dbContext.Designations.AnyAsync(d => d.Title == desigTitle))
        {
            dbContext.Designations.Add(new Designation { Title = desigTitle });
        }
    }
    await dbContext.SaveChangesAsync();

    // Ensure default Branches exist
    var headOfficeBranch = await dbContext.Branches.FirstOrDefaultAsync(b => b.Name == "Head Office" || b.Name == "Head Office - Colombo" || b.Name.Contains("Head Office"));
    if (headOfficeBranch == null)
    {
        headOfficeBranch = new Branch
        {
            Name = "Head Office",
            Location = "Colombo"
        };
        dbContext.Branches.Add(headOfficeBranch);
        await dbContext.SaveChangesAsync();
    }

    // Ensure default Departments exist
    var hrDepartment = await dbContext.Departments.FirstOrDefaultAsync(d => d.Name == "Human Resources" || d.Name == "HR");
    if (hrDepartment == null)
    {
        hrDepartment = new Department
        {
            Name = "Human Resources"
        };
        dbContext.Departments.Add(hrDepartment);
        await dbContext.SaveChangesAsync();
    }

    var welfareDepartment = await dbContext.Departments.FirstOrDefaultAsync(d => d.Name == "Welfare");
    if (welfareDepartment == null)
    {
        welfareDepartment = new Department
        {
            Name = "Welfare"
        };
        dbContext.Departments.Add(welfareDepartment);
        await dbContext.SaveChangesAsync();
    }

    var managerialDepartment = await dbContext.Departments.FirstOrDefaultAsync(d => d.Name == "Managerial" || d.Name == "Management");
    if (managerialDepartment == null)
    {
        managerialDepartment = new Department
        {
            Name = "Managerial"
        };
        dbContext.Departments.Add(managerialDepartment);
        await dbContext.SaveChangesAsync();
    }

    // Link Head Office to Human Resources and Welfare in BranchDepartments
    if (!await dbContext.BranchDepartments.AnyAsync(bd => bd.BranchId == headOfficeBranch.Id && bd.DepartmentId == hrDepartment.Id))
    {
        dbContext.BranchDepartments.Add(new BranchDepartment
        {
            BranchId = headOfficeBranch.Id,
            DepartmentId = hrDepartment.Id
        });
    }

    if (!await dbContext.BranchDepartments.AnyAsync(bd => bd.BranchId == headOfficeBranch.Id && bd.DepartmentId == welfareDepartment.Id))
    {
        dbContext.BranchDepartments.Add(new BranchDepartment
        {
            BranchId = headOfficeBranch.Id,
            DepartmentId = welfareDepartment.Id
        });
    }

    // Link all branches to Managerial department in BranchDepartments
    var allBranchesList = await dbContext.Branches.ToListAsync();
    foreach (var br in allBranchesList)
    {
        if (!await dbContext.BranchDepartments.AnyAsync(bd => bd.BranchId == br.Id && bd.DepartmentId == managerialDepartment.Id))
        {
            dbContext.BranchDepartments.Add(new BranchDepartment
            {
                BranchId = br.Id,
                DepartmentId = managerialDepartment.Id
            });
        }
    }
    // Seed default Welfare Types if missing
    try
    {
        var defaultWelfareTypes = new[]
        {
            new WelfareType { TypeName = "Medical Assistance", Category = "Health & Welfare", MaxEligibleAmount = 100000m, IsActive = true, CreatedAt = DateTime.Now },
            new WelfareType { TypeName = "Education Assistance", Category = "Education", MaxEligibleAmount = 50000m, IsActive = true, CreatedAt = DateTime.Now },
            new WelfareType { TypeName = "Housing Loan", Category = "Housing", MaxEligibleAmount = 500000m, IsActive = true, CreatedAt = DateTime.Now },
            new WelfareType { TypeName = "Festival Advance", Category = "Financial", MaxEligibleAmount = 25000m, IsActive = true, CreatedAt = DateTime.Now },
            new WelfareType { TypeName = "Funeral Assistance", Category = "Emergency", MaxEligibleAmount = 30000m, IsActive = true, CreatedAt = DateTime.Now }
        };

        foreach (var wt in defaultWelfareTypes)
        {
            if (!await dbContext.WelfareTypes.AnyAsync(w => w.TypeName == wt.TypeName))
            {
                dbContext.WelfareTypes.Add(wt);
            }
        }
        await dbContext.SaveChangesAsync();
    }
    catch { }

    // Ensure default Welfare Manager duty account exists
    try
    {
        var headOfWelfareUser = await userManager.FindByNameAsync("head.welfare");
        if (headOfWelfareUser == null)
        {
            var welfareDeptHeadEmp = await dbContext.Employees.FirstOrDefaultAsync(e => e.Email == "head.welfare@kanrich.lk");
            if (welfareDeptHeadEmp == null)
            {
                var desigWelfare = await dbContext.Designations.FirstOrDefaultAsync(d => d.Title == "Welfare Manager")
                                  ?? await dbContext.Designations.FirstOrDefaultAsync(d => d.Title == "Department Head");
                welfareDeptHeadEmp = new Employee
                {
                    FullName           = "Welfare Manager",
                    Initials           = "WM",
                    NIC                = "DUTY-WLF",
                    DateOfBirth        = new DateTime(1990, 1, 1),
                    Sex                = "N/A",
                    PhoneNumber        = "0112345678",
                    ResidentialAddress = "-",
                    EmployeeType       = "Permanent",
                    EPFNumber          = "DUTY-WLF-01",
                    ETFNumber          = "DUTY-WLF-01",
                    BankAccountName    = "-",
                    BankAccountNumber  = "-",
                    Email              = "head.welfare@kanrich.lk",
                    DateJoined         = DateTime.Now,
                    Status             = "Active",
                    DepartmentId       = welfareDepartment.Id,
                    DesignationId      = desigWelfare?.Id,
                    BranchId           = headOfficeBranch.Id
                };
                dbContext.Employees.Add(welfareDeptHeadEmp);
                await dbContext.SaveChangesAsync();
            }

            headOfWelfareUser = new ApplicationUser
            {
                UserName = "head.welfare",
                Email = "head.welfare@kanrich.lk",
                EmailConfirmed = true,
                FullName = "Welfare Manager",
                EpfNumber = "DUTY-WLF-01",
                Branch = headOfficeBranch.Name,
                Department = welfareDepartment.Name,
                Designation = "Welfare Manager",
                EmployeeId = welfareDeptHeadEmp.Id,
                MustChangePassword = false
            };

            var createRes = await userManager.CreateAsync(headOfWelfareUser, "Welfare@123");
            if (createRes.Succeeded)
            {
                await userManager.AddToRoleAsync(headOfWelfareUser, "Welfare Manager");
            }
        }
        else
        {
            var welfareDeptHeadEmp = headOfWelfareUser.EmployeeId.HasValue
                ? await dbContext.Employees.FindAsync(headOfWelfareUser.EmployeeId.Value)
                : await dbContext.Employees.FirstOrDefaultAsync(e => e.Email == "head.welfare@kanrich.lk");

            if (welfareDeptHeadEmp == null)
            {
                var desigWelfare = await dbContext.Designations.FirstOrDefaultAsync(d => d.Title == "Welfare Manager")
                                  ?? await dbContext.Designations.FirstOrDefaultAsync(d => d.Title == "Department Head");
                welfareDeptHeadEmp = new Employee
                {
                    FullName           = "Welfare Manager",
                    Initials           = "WM",
                    NIC                = "DUTY-WLF",
                    DateOfBirth        = new DateTime(1990, 1, 1),
                    Sex                = "N/A",
                    PhoneNumber        = "0112345678",
                    ResidentialAddress = "-",
                    EmployeeType       = "Permanent",
                    EPFNumber          = "DUTY-WLF-01",
                    ETFNumber          = "DUTY-WLF-01",
                    BankAccountName    = "-",
                    BankAccountNumber  = "-",
                    Email              = "head.welfare@kanrich.lk",
                    DateJoined         = DateTime.Now,
                    Status             = "Active",
                    DepartmentId       = welfareDepartment.Id,
                    DesignationId      = desigWelfare?.Id,
                    BranchId           = headOfficeBranch.Id
                };
                dbContext.Employees.Add(welfareDeptHeadEmp);
                await dbContext.SaveChangesAsync();
            }
            else if (welfareDeptHeadEmp.FullName != "Welfare Manager")
            {
                welfareDeptHeadEmp.FullName = "Welfare Manager";
                welfareDeptHeadEmp.Initials = "WM";
                await dbContext.SaveChangesAsync();
            }

            bool needsUserUpdate = false;
            if (headOfWelfareUser.EmployeeId != welfareDeptHeadEmp.Id)
            {
                headOfWelfareUser.EmployeeId = welfareDeptHeadEmp.Id;
                needsUserUpdate = true;
            }
            if (headOfWelfareUser.FullName != "Welfare Manager")
            {
                headOfWelfareUser.FullName = "Welfare Manager";
                headOfWelfareUser.Designation = "Welfare Manager";
                needsUserUpdate = true;
            }
            if (needsUserUpdate)
            {
                await userManager.UpdateAsync(headOfWelfareUser);
            }

            if (!await userManager.IsInRoleAsync(headOfWelfareUser, "Welfare Manager"))
            {
                await userManager.AddToRoleAsync(headOfWelfareUser, "Welfare Manager");
            }
            if (await userManager.IsInRoleAsync(headOfWelfareUser, "Head of Welfare"))
            {
                await userManager.RemoveFromRoleAsync(headOfWelfareUser, "Head of Welfare");
            }
            if (await userManager.IsInRoleAsync(headOfWelfareUser, "Department Head"))
            {
                await userManager.RemoveFromRoleAsync(headOfWelfareUser, "Department Head");
            }
        }
    }
    catch { }

    // Ensure MustChangePassword column exists in AspNetUsers
    try
    {
        if (isSqlServer)
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                IF COL_LENGTH('AspNetUsers', 'MustChangePassword') IS NULL
                    ALTER TABLE AspNetUsers ADD MustChangePassword bit NOT NULL DEFAULT 1;
            ");
        }
        else
        {
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(@"
                    ALTER TABLE `AspNetUsers` ADD COLUMN `MustChangePassword` tinyint(1) NOT NULL DEFAULT 1;
                ");
            }
            catch { }
        }
    }
    catch { }

    // Seed default admin user
    const string adminUsername = "admin";
    const string adminEmail = "admin@kanrich.lk";
    const string adminPassword = "Admin@123";

    var existingAdmin = await userManager.FindByNameAsync(adminUsername) ?? await userManager.FindByEmailAsync(adminEmail);
    if (existingAdmin is null)
    {
        var adminUser = new ApplicationUser
        {
            UserName = adminUsername,
            Email = adminEmail,
            EmailConfirmed = true,
            FullName = "System Administrator",
            EpfNumber = "EPF-0000",
            Branch = headOfficeBranch.Name,
            Designation = "System Administrator",
            Department = "IT",
            MustChangePassword = true
        };
        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
    else
    {
        if (!await userManager.IsInRoleAsync(existingAdmin, "Admin"))
        {
            await userManager.AddToRoleAsync(existingAdmin, "Admin");
        }
    }

    // Normalize user roles: real employees (NIC != 'DUTY-ACC') should have the 'Employee' role only
    try
    {
        var realEmployeeIds = await dbContext.Employees
            .Where(e => !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC")
            .Select(e => e.Id)
            .ToListAsync();

        var realEmpUsers = await userManager.Users
            .Where(u => u.EmployeeId != null && realEmployeeIds.Contains(u.EmployeeId.Value))
            .ToListAsync();

        foreach (var empUser in realEmpUsers)
        {
            var userRoles = await userManager.GetRolesAsync(empUser);
            var dutyRoles = userRoles.Where(r => r == "Department Head" || r == "Branch Manager" || r == "Area Manager").ToList();
            if (dutyRoles.Any())
            {
                await userManager.RemoveFromRolesAsync(empUser, dutyRoles);
            }
            if (!userRoles.Contains("Employee"))
            {
                await userManager.AddToRoleAsync(empUser, "Employee");
            }
        }
    }
    catch { }
}
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[Startup] Database initialization or seeding encountered an error: {ex}");
}

// Configure HTTP pipeline
app.UseDeveloperExceptionPage();
app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.MapGet("/api/documents/download/{id:int}", async (int id, HttpContext httpContext, IDeathService deathService, string? mode) =>
{
    var result = await deathService.DownloadDocumentAsync(id);
    if (result == null) return Results.NotFound();

    var (content, contentType, fileName) = result.Value;
    var effectiveContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;

    if (mode == "view")
    {
        httpContext.Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
        return Results.File(content, effectiveContentType);
    }

    return Results.File(content, effectiveContentType, fileName);
}).RequireAuthorization();

app.MapGet("/setup-db", async (IServiceProvider services) =>
{
    using var scope = services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    var result = new Dictionary<string, object?>();
    try
    {
        var cs = context.Database.GetConnectionString();
        var safeCs = cs != null ? System.Text.RegularExpressions.Regex.Replace(cs, "Password=[^;]+", "Password=***") : "NULL";
        result["ConnectionString"] = safeCs;
        result["CanConnect"] = await context.Database.CanConnectAsync();

        var creator = context.Database.GetService<IDatabaseCreator>() as IRelationalDatabaseCreator;
        if (creator != null)
        {
            result["HasTablesBefore"] = await creator.HasTablesAsync();
            if (!await creator.HasTablesAsync())
            {
                var ddlScript = context.Database.GenerateCreateScript();
                ddlScript = System.Text.RegularExpressions.Regex.Replace(ddlScript, @"\bON\s+DELETE\s+CASCADE\b", "ON DELETE NO ACTION", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var batches = ddlScript.Split(new[] { "\r\nGO\r\n", "\nGO\n", "\r\nGO", "\nGO" }, StringSplitOptions.RemoveEmptyEntries);
                var batchErrors = new List<string>();
                foreach (var batch in batches)
                {
                    var trimmed = batch.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        try
                        {
                            await context.Database.ExecuteSqlRawAsync(trimmed);
                        }
                        catch (Exception ex)
                        {
                            batchErrors.Add(ex.Message);
                        }
                    }
                }
                result["DdlExecutionErrors"] = batchErrors;
            }
            result["HasTablesAfter"] = await creator.HasTablesAsync();
        }

        // Seed roles
        string[] roles = ["Admin", "HR Manager", "HR Officer", "Employee", "Area Manager", "Branch Manager", "Department Head"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var admin = await userManager.FindByNameAsync("admin");
        if (admin == null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = "admin",
                Email = "admin@kanrich.lk",
                EmailConfirmed = true,
                FullName = "System Administrator",
                EpfNumber = "EPF-0000",
                Branch = "Head Office",
                Designation = "System Administrator",
                Department = "IT"
            };
            var createRes = await userManager.CreateAsync(adminUser, "Admin@123");
            result["AdminCreated"] = createRes.Succeeded;
            if (createRes.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
            else
            {
                result["AdminCreateErrors"] = createRes.Errors.Select(e => e.Description);
            }
        }
        else
        {
            result["AdminExists"] = true;
            if (!await userManager.IsInRoleAsync(admin, "Admin"))
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }

        result["Status"] = "Success";
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        result["Status"] = "Error";
        result["Exception"] = ex.ToString();
        return Results.Json(result, statusCode: 500);
    }
});

app.Run();
