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

var builder = WebApplication.CreateBuilder(args);

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 0))
    )
);

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
        policy.RequireRole("Admin", "HR Manager"));
    options.AddPolicy("RequireManagers", policy =>
        policy.RequireRole("HR Manager", "Area Manager", "Branch Manager"));
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

// Add Razor Pages with role-based folder authorization
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Settings", "RequireAdmin");
    options.Conventions.AuthorizeFolder("/Employees", "RequireManagers");
    options.Conventions.AuthorizeFolder("/Documents", "RequireAdminOrHR");
    options.Conventions.AuthorizeFolder("/Employee", "RequireEmployee");
});

var app = builder.Build();

// Ensure database exists, then apply migrations
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    var connectionString = context.Database.GetConnectionString();
    if (!string.IsNullOrEmpty(connectionString))
    {
        try
        {
            var csBuilder = new MySqlConnectionStringBuilder(connectionString);
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
            // Cloud providers like Aiven may deny CREATE DATABASE permissions.
            // That's fine because the 'defaultdb' database already exists. We can safely ignore this.
            Console.WriteLine("Could not manually create database, continuing to EnsureCreated. Error: " + ex.Message);
        }
    }

    await context.Database.EnsureCreatedAsync();

    // Repair columns that may be missing due to a partial migration failure or schema drift
    using (var connection = new MySqlConnection(context.Database.GetConnectionString()))
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

        // EmployeeId was dropped by a partial migration that failed mid-way
        await AddColumnIfMissing("AspNetUsers", "EmployeeId", "int NULL");

        // JoinDate added for service duration display
        await AddColumnIfMissing("TransferRequests", "JoinDate", "datetime(6) NULL");

        // DeptHead fields added for 5-stage transfer workflow
        await AddColumnIfMissing("TransferRequests", "DeptHeadReview", "varchar(50) NULL");
        await AddColumnIfMissing("TransferRequests", "DeptHeadReviewDate", "datetime(6) NULL");
        await AddColumnIfMissing("TransferRequests", "DeptHeadComments", "varchar(1000) NULL");

        // Leave fields added for Imasha's modules
        await AddColumnIfMissing("Leaves", "AppliedDate", "datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)");
        await AddColumnIfMissing("Leaves", "TotalDays", "int NOT NULL DEFAULT 0");
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
        await AddColumnIfMissing("MaternityPayments", "ProcessedBy", "longtext NULL");
        
        // Modify PaymentDate to be nullable
        var alterDate = connection.CreateCommand();
        alterDate.CommandText = "ALTER TABLE `MaternityPayments` MODIFY COLUMN `PaymentDate` datetime(6) NULL";
        try { await alterDate.ExecuteNonQueryAsync(); } catch {}

        // Create LeaveAllocationSettings table if not exists
        var createTableCmd = connection.CreateCommand();
        createTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS `LeaveAllocationSettings` (
                `Id` int AUTO_INCREMENT PRIMARY KEY,
                `LeaveType` varchar(50) NOT NULL UNIQUE,
                `DefaultDays` int NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
        try { await createTableCmd.ExecuteNonQueryAsync(); } catch {}

        // Attendance fields
        await AddColumnIfMissing("Attendances", "TotalHours", "double NULL");

        // Apply missing Area Manager managed branches for existing seeded users


        var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = "UPDATE `AspNetUsers` SET `ManagedBranches` = '2,4' WHERE `FullName` = 'Nimal Perera' AND (`ManagedBranches` IS NULL OR `ManagedBranches` = '')";
        await updateCmd.ExecuteNonQueryAsync();

        var updateCmd2 = connection.CreateCommand();
        updateCmd2.CommandText = "UPDATE `AspNetUsers` SET `ManagedBranches` = '3' WHERE `FullName` = 'Suresh Fernando' AND (`ManagedBranches` IS NULL OR `ManagedBranches` = '')";
        await updateCmd2.ExecuteNonQueryAsync();
    }
}

// Seed roles and default users
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    // Seed roles
    string[] roles = ["Admin", "HR Manager", "Employee", "Area Manager", "Branch Manager", "Department Head"];
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    // Seed default admin user
    const string adminEmail = "admin@kanrich.lk";
    const string adminPassword = "Admin@123";

    if (await userManager.FindByEmailAsync(adminEmail) is null)
    {
        var adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            FullName = "System Administrator",
            EpfNumber = "EPF-0000",
            Branch = "Head Office - Colombo",
            Designation = "System Administrator",
            Department = "IT"
        };
        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(adminUser, "Admin");
    }
}

// Configure HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
