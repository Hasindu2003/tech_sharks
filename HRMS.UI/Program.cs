using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.UI.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
    )
);

// Add ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    options.User.RequireUniqueEmail = true;

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

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Add services
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ITransferRequestService, TransferRequestService>();

builder.Services.AddRazorPages();

var app = builder.Build();

// Ensure database exists, then apply migrations
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    var connectionString = context.Database.GetConnectionString();
    if (!string.IsNullOrEmpty(connectionString))
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

    await context.Database.MigrateAsync();
}

// Seed roles and dummy users
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    // Seed roles
    string[] roles = ["Area Manager", "HR Manager", "Branch Manager", "Employee"];
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    // Seed dummy users with @kanrich.lk emails
    var dummyUsers = new[]
    {
        // ══════════════════════════════════════════════════════════════
        //  AREA MANAGERS — Head Office, Colombo
        // ══════════════════════════════════════════════════════════════
        new { Email = "nimal.perera@kanrich.lk",              Password = "Kanrich@2024", Role = "Area Manager",   FullName = "Nimal Perera",              EpfNumber = "EPF-1001", Branch = "Head Office - Colombo", Designation = "Area Manager - Western",  Department = "Management",      DateOfJoining = new DateTime(2010, 3, 15) },
        new { Email = "suresh.fernando@kanrich.lk",           Password = "Kanrich@2024", Role = "Area Manager",   FullName = "Suresh Fernando",           EpfNumber = "EPF-1002", Branch = "Head Office - Colombo", Designation = "Area Manager - Central",  Department = "Management",      DateOfJoining = new DateTime(2011, 7, 1) },

        // ══════════════════════════════════════════════════════════════
        //  HR MANAGERS — Head Office, Colombo
        // ══════════════════════════════════════════════════════════════
        new { Email = "priyantha.jayasekara@kanrich.lk",      Password = "Kanrich@2024", Role = "HR Manager",     FullName = "Priyantha Jayasekara",      EpfNumber = "EPF-1501", Branch = "Head Office - Colombo", Designation = "Senior HR Manager",       Department = "Human Resources", DateOfJoining = new DateTime(2012, 4, 1) },
        new { Email = "nimali.wickramasinghe@kanrich.lk",     Password = "Kanrich@2024", Role = "HR Manager",     FullName = "Nimali Wickramasinghe",     EpfNumber = "EPF-1502", Branch = "Head Office - Colombo", Designation = "HR Manager",              Department = "Human Resources", DateOfJoining = new DateTime(2016, 8, 15) },

        // ══════════════════════════════════════════════════════════════
        //  BRANCH MANAGERS
        // ══════════════════════════════════════════════════════════════
        new { Email = "kamani.silva@kanrich.lk",              Password = "Kanrich@2024", Role = "Branch Manager", FullName = "Kamani Silva",              EpfNumber = "EPF-2001", Branch = "Kandy Branch",          Designation = "Branch Manager",          Department = "Operations",      DateOfJoining = new DateTime(2013, 1, 10) },
        new { Email = "roshan.jayawardena@kanrich.lk",        Password = "Kanrich@2024", Role = "Branch Manager", FullName = "Roshan Jayawardena",        EpfNumber = "EPF-2002", Branch = "Galle Branch",          Designation = "Branch Manager",          Department = "Operations",      DateOfJoining = new DateTime(2014, 5, 20) },
        new { Email = "dilini.rathnayake@kanrich.lk",         Password = "Kanrich@2024", Role = "Branch Manager", FullName = "Dilini Rathnayake",         EpfNumber = "EPF-2003", Branch = "Negombo Branch",        Designation = "Branch Manager",          Department = "Operations",      DateOfJoining = new DateTime(2015, 9, 1) },

        // ══════════════════════════════════════════════════════════════
        //  KANDY BRANCH — Finance Department (3)
        // ══════════════════════════════════════════════════════════════
        new { Email = "kasun.bandara@kanrich.lk",             Password = "Kanrich@2024", Role = "Employee",       FullName = "Kasun Bandara",             EpfNumber = "EPF-3001", Branch = "Kandy Branch",          Designation = "Senior Executive",        Department = "Finance",         DateOfJoining = new DateTime(2018, 4, 12) },
        new { Email = "anjali.fernando@kanrich.lk",           Password = "Kanrich@2024", Role = "Employee",       FullName = "Anjali Fernando",           EpfNumber = "EPF-3002", Branch = "Kandy Branch",          Designation = "Executive",               Department = "Finance",         DateOfJoining = new DateTime(2019, 9, 5) },
        new { Email = "ruwan.pathirana@kanrich.lk",           Password = "Kanrich@2024", Role = "Employee",       FullName = "Ruwan Pathirana",           EpfNumber = "EPF-3003", Branch = "Kandy Branch",          Designation = "Junior Executive",        Department = "Finance",         DateOfJoining = new DateTime(2022, 1, 15) },

        // KANDY BRANCH — HR Department (3)
        new { Email = "malini.herath@kanrich.lk",             Password = "Kanrich@2024", Role = "Employee",       FullName = "Malini Herath",             EpfNumber = "EPF-3004", Branch = "Kandy Branch",          Designation = "Senior Executive",        Department = "HR",              DateOfJoining = new DateTime(2019, 6, 1) },
        new { Email = "sachini.perera@kanrich.lk",            Password = "Kanrich@2024", Role = "Employee",       FullName = "Sachini Perera",            EpfNumber = "EPF-3005", Branch = "Kandy Branch",          Designation = "Executive",               Department = "HR",              DateOfJoining = new DateTime(2020, 3, 10) },
        new { Email = "dinesh.rajapaksha@kanrich.lk",         Password = "Kanrich@2024", Role = "Employee",       FullName = "Dinesh Rajapaksha",         EpfNumber = "EPF-3006", Branch = "Kandy Branch",          Designation = "Junior Executive",        Department = "HR",              DateOfJoining = new DateTime(2023, 2, 20) },

        // KANDY BRANCH — IT Department (3)
        new { Email = "lakmal.jayasuriya@kanrich.lk",         Password = "Kanrich@2024", Role = "Employee",       FullName = "Lakmal Jayasuriya",         EpfNumber = "EPF-3007", Branch = "Kandy Branch",          Designation = "Senior Executive",        Department = "IT",              DateOfJoining = new DateTime(2017, 8, 1) },
        new { Email = "nethmi.silva@kanrich.lk",              Password = "Kanrich@2024", Role = "Employee",       FullName = "Nethmi Silva",              EpfNumber = "EPF-3008", Branch = "Kandy Branch",          Designation = "Executive",               Department = "IT",              DateOfJoining = new DateTime(2020, 11, 15) },
        new { Email = "ashan.wijeratne@kanrich.lk",           Password = "Kanrich@2024", Role = "Employee",       FullName = "Ashan Wijeratne",           EpfNumber = "EPF-3009", Branch = "Kandy Branch",          Designation = "Junior Executive",        Department = "IT",              DateOfJoining = new DateTime(2022, 6, 8) },

        // ══════════════════════════════════════════════════════════════
        //  GALLE BRANCH — Finance Department (3)
        // ══════════════════════════════════════════════════════════════
        new { Email = "tharaka.wijesinghe@kanrich.lk",        Password = "Kanrich@2024", Role = "Employee",       FullName = "Tharaka Wijesinghe",        EpfNumber = "EPF-3010", Branch = "Galle Branch",          Designation = "Senior Executive",        Department = "Finance",         DateOfJoining = new DateTime(2017, 2, 15) },
        new { Email = "chamari.bandara@kanrich.lk",           Password = "Kanrich@2024", Role = "Employee",       FullName = "Chamari Bandara",           EpfNumber = "EPF-3011", Branch = "Galle Branch",          Designation = "Executive",               Department = "Finance",         DateOfJoining = new DateTime(2019, 7, 20) },
        new { Email = "janith.kumara@kanrich.lk",             Password = "Kanrich@2024", Role = "Employee",       FullName = "Janith Kumara",             EpfNumber = "EPF-3012", Branch = "Galle Branch",          Designation = "Junior Executive",        Department = "Finance",         DateOfJoining = new DateTime(2023, 4, 1) },

        // GALLE BRANCH — HR Department (3)
        new { Email = "sanduni.gamage@kanrich.lk",            Password = "Kanrich@2024", Role = "Employee",       FullName = "Sanduni Gamage",            EpfNumber = "EPF-3013", Branch = "Galle Branch",          Designation = "Senior Executive",        Department = "HR",              DateOfJoining = new DateTime(2018, 10, 5) },
        new { Email = "prasanna.dissanayake@kanrich.lk",      Password = "Kanrich@2024", Role = "Employee",       FullName = "Prasanna Dissanayake",      EpfNumber = "EPF-3014", Branch = "Galle Branch",          Designation = "Executive",               Department = "HR",              DateOfJoining = new DateTime(2021, 1, 18) },
        new { Email = "iresha.gunathilaka@kanrich.lk",        Password = "Kanrich@2024", Role = "Employee",       FullName = "Iresha Gunathilaka",        EpfNumber = "EPF-3015", Branch = "Galle Branch",          Designation = "Junior Executive",        Department = "HR",              DateOfJoining = new DateTime(2023, 8, 12) },

        // GALLE BRANCH — IT Department (3)
        new { Email = "nuwan.herath@kanrich.lk",              Password = "Kanrich@2024", Role = "Employee",       FullName = "Nuwan Herath",              EpfNumber = "EPF-3016", Branch = "Galle Branch",          Designation = "Senior Executive",        Department = "IT",              DateOfJoining = new DateTime(2016, 5, 10) },
        new { Email = "dilhani.rathnayake@kanrich.lk",        Password = "Kanrich@2024", Role = "Employee",       FullName = "Dilhani Rathnayake",        EpfNumber = "EPF-3017", Branch = "Galle Branch",          Designation = "Executive",               Department = "IT",              DateOfJoining = new DateTime(2020, 9, 22) },
        new { Email = "sampath.wickramasinghe@kanrich.lk",    Password = "Kanrich@2024", Role = "Employee",       FullName = "Sampath Wickramasinghe",    EpfNumber = "EPF-3018", Branch = "Galle Branch",          Designation = "Junior Executive",        Department = "IT",              DateOfJoining = new DateTime(2022, 12, 1) },

        // ══════════════════════════════════════════════════════════════
        //  NEGOMBO BRANCH — Finance Department (3)
        // ══════════════════════════════════════════════════════════════
        new { Email = "chathura.mendis@kanrich.lk",           Password = "Kanrich@2024", Role = "Employee",       FullName = "Chathura Mendis",           EpfNumber = "EPF-3019", Branch = "Negombo Branch",        Designation = "Senior Executive",        Department = "Finance",         DateOfJoining = new DateTime(2020, 1, 5) },
        new { Email = "harsha.senaratne@kanrich.lk",          Password = "Kanrich@2024", Role = "Employee",       FullName = "Harsha Senaratne",          EpfNumber = "EPF-3020", Branch = "Negombo Branch",        Designation = "Executive",               Department = "Finance",         DateOfJoining = new DateTime(2021, 6, 15) },
        new { Email = "miyuri.jayawardena@kanrich.lk",        Password = "Kanrich@2024", Role = "Employee",       FullName = "Miyuri Jayawardena",        EpfNumber = "EPF-3021", Branch = "Negombo Branch",        Designation = "Junior Executive",        Department = "Finance",         DateOfJoining = new DateTime(2023, 3, 8) },

        // NEGOMBO BRANCH — HR Department (3)
        new { Email = "piumi.dissanayake@kanrich.lk",         Password = "Kanrich@2024", Role = "Employee",       FullName = "Piumi Dissanayake",         EpfNumber = "EPF-3022", Branch = "Negombo Branch",        Designation = "Senior Executive",        Department = "HR",              DateOfJoining = new DateTime(2019, 4, 20) },
        new { Email = "ravindu.silva@kanrich.lk",             Password = "Kanrich@2024", Role = "Employee",       FullName = "Ravindu Silva",             EpfNumber = "EPF-3023", Branch = "Negombo Branch",        Designation = "Executive",               Department = "HR",              DateOfJoining = new DateTime(2021, 11, 1) },
        new { Email = "kumuduni.fernando@kanrich.lk",         Password = "Kanrich@2024", Role = "Employee",       FullName = "Kumuduni Fernando",         EpfNumber = "EPF-3024", Branch = "Negombo Branch",        Designation = "Junior Executive",        Department = "HR",              DateOfJoining = new DateTime(2024, 1, 10) },

        // NEGOMBO BRANCH — IT Department (3)
        new { Email = "sameera.gunaratne@kanrich.lk",         Password = "Kanrich@2024", Role = "Employee",       FullName = "Sameera Gunaratne",         EpfNumber = "EPF-3025", Branch = "Negombo Branch",        Designation = "Senior Executive",        Department = "IT",              DateOfJoining = new DateTime(2018, 7, 15) },
        new { Email = "thilini.perera@kanrich.lk",            Password = "Kanrich@2024", Role = "Employee",       FullName = "Thilini Perera",            EpfNumber = "EPF-3026", Branch = "Negombo Branch",        Designation = "Executive",               Department = "IT",              DateOfJoining = new DateTime(2021, 2, 28) },
        new { Email = "asanka.jayasinghe@kanrich.lk",         Password = "Kanrich@2024", Role = "Employee",       FullName = "Asanka Jayasinghe",         EpfNumber = "EPF-3027", Branch = "Negombo Branch",        Designation = "Junior Executive",        Department = "IT",              DateOfJoining = new DateTime(2023, 9, 5) },
    };

    foreach (var dummy in dummyUsers)
    {
        if (await userManager.FindByEmailAsync(dummy.Email) is null)
        {
            var user = new ApplicationUser
            {
                UserName = dummy.Email,
                Email = dummy.Email,
                EmailConfirmed = true,
                FullName = dummy.FullName,
                EpfNumber = dummy.EpfNumber,
                Branch = dummy.Branch,
                Designation = dummy.Designation,
                Department = dummy.Department,
                DateOfJoining = dummy.DateOfJoining
            };
            var result = await userManager.CreateAsync(user, dummy.Password);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(user, dummy.Role);
        }
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
