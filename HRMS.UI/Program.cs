using HRMS.Application.Attendance;
using HRMS.Application.Leave;
using HRMS.Application.Notifications;
using HRMS.Application.Payroll;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

// Add Razor Pages
builder.Services.AddRazorPages();

// Add API controllers (used for the Attendance punch endpoints)
builder.Services.AddControllers();

// Attendance shift rules — configurable via the "AttendanceShift" config section,
// falls back to the class defaults (09:00-18:00, 15 min grace) if not set.
var shiftOptions = new AttendanceShiftOptions();
builder.Configuration.GetSection("AttendanceShift").Bind(shiftOptions);
builder.Services.AddSingleton(shiftOptions);

builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<ILeaveService, LeaveService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IEmailSender, NoOpEmailSender>();
builder.Services.AddScoped<IPayrollLeaveExportService, PayrollLeaveExportService>();

var app = builder.Build();

// Seed roles and default admin user
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    // Seed roles
    string[] roles = ["Admin", "HR Manager", "Employee"];
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
            EmailConfirmed = true,
            MustChangePassword = false
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
            EmailConfirmed = true,
            MustChangePassword = false
        };
        var hrResult = await userManager.CreateAsync(hrUser, hrPassword);
        if (hrResult.Succeeded)
            await userManager.AddToRoleAsync(hrUser, "HR Manager");
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

// Force a redirect to the change-password page until a newly-provisioned account
// (see Employees/Index "Create Login") has replaced its temporary password.
app.Use(async (HttpContext context, Func<Task> next) =>
{
    if (context.User.Identity?.IsAuthenticated == true
        && !context.Request.Path.StartsWithSegments("/Account")
        && !context.Request.Path.StartsWithSegments("/api"))
    {
        var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.GetUserAsync(context.User);
        if (user?.MustChangePassword == true)
        {
            context.Response.Redirect("/Account/ChangePassword");
            return;
        }
    }

    await next();
});

app.MapRazorPages();
app.MapControllers();

app.Run();
