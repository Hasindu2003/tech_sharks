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
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure login/logout paths
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy =>
        policy.RequireRole("Admin"));
    options.AddPolicy("RequireAdminOrHR", policy =>
        policy.RequireRole("Admin", "HR Manager"));
    options.AddPolicy("RequireManagers", policy =>
        policy.RequireRole("HR Manager", "Area Manager", "Branch Manager"));
});

// Register command handlers
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

// Add Razor Pages
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Settings", "RequireAdmin");
    options.Conventions.AuthorizeFolder("/Employees", "RequireManagers");
    options.Conventions.AuthorizeFolder("/Documents", "RequireAdminOrHR");
});



var app = builder.Build();

// Seed roles and default admin user
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
    const string adminEmail = "admin@hrms.local";
    const string adminPassword = "Admin@123";

    if (await userManager.FindByEmailAsync(adminEmail) is null)
    {
        var adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(adminUser, "Admin");
    }

    // Seed default HR Manager user
    const string hrEmail = "hr@hrms.local";
    const string hrPassword = "HrManager@123";

    if (await userManager.FindByEmailAsync(hrEmail) is null)
    {
        var hrUser = new ApplicationUser
        {
            UserName = hrEmail,
            Email = hrEmail,
            EmailConfirmed = true
        };
        var hrResult = await userManager.CreateAsync(hrUser, hrPassword);
        if (hrResult.Succeeded)
            await userManager.AddToRoleAsync(hrUser, "HR Manager");
    }

    // Seed default Employee user
    const string employeeEmail = "employee@hrms.local";
    const string employeePassword = "Employee@123";

    if (await userManager.FindByEmailAsync(employeeEmail) is null)
    {
        var employeeUser = new ApplicationUser
        {
            UserName = employeeEmail,
            Email = employeeEmail,
            EmailConfirmed = true
        };
        var employeeResult = await userManager.CreateAsync(employeeUser, employeePassword);
        if (employeeResult.Succeeded)
            await userManager.AddToRoleAsync(employeeUser, "Employee");
    }

    // Seed default Area Manager user
    const string areaManagerEmail = "area@hrms.local";
    const string areaManagerPassword = "AreaManager@123";

    if (await userManager.FindByEmailAsync(areaManagerEmail) is null)
    {
        var areaUser = new ApplicationUser
        {
            UserName = areaManagerEmail, Email = areaManagerEmail, EmailConfirmed = true
        };
        var areaResult = await userManager.CreateAsync(areaUser, areaManagerPassword);
        if (areaResult.Succeeded)
            await userManager.AddToRoleAsync(areaUser, "Area Manager");
    }

    // Seed default Branch Manager user
    const string branchManagerEmail = "branch@hrms.local";
    const string branchManagerPassword = "BranchManager@123";

    if (await userManager.FindByEmailAsync(branchManagerEmail) is null)
    {
        var branchUser = new ApplicationUser
        {
            UserName = branchManagerEmail, Email = branchManagerEmail, EmailConfirmed = true
        };
        var branchResult = await userManager.CreateAsync(branchUser, branchManagerPassword);
        if (branchResult.Succeeded)
            await userManager.AddToRoleAsync(branchUser, "Branch Manager");
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
