using HRMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Persistence
{
    public static class DatabaseSeeder
    {
        public static async Task SeedRolesAndUsersAsync(
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager)
        {
            // ── 1. Seed Roles ─────────────────────────────────────────────────
            string[] roles = { "Employee", "BranchDGM", "HODGM", "SeniorManagement", "Finance", "Admin" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                    if (!roleResult.Succeeded)
                    {
                        throw new Exception($"Failed to create role '{role}': " +
                            string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                    }
                }
            }

            // ── 2. Seed Test Users ────────────────────────────────────────────
            const string password = "Kanrich@2024";
            var testUsers = new[]
            {
                new { Email = "kasun.bandara@kanrich.lk",        FullName = "Kasun Bandara",        Role = "Employee"         },
                new { Email = "kamani.silva@kanrich.lk",         FullName = "Kamani Silva",         Role = "BranchDGM"        },
                new { Email = "nimal.perera@kanrich.lk",         FullName = "Nimal Perera",         Role = "HODGM"            },
                new { Email = "priyantha.jayasekara@kanrich.lk", FullName = "Priyantha Jayasekara", Role = "SeniorManagement" },
                new { Email = "finance.officer@kanrich.lk",      FullName = "Finance Officer",      Role = "Finance"          },
                new { Email = "admin@kanrich.lk",                FullName = "System Admin",         Role = "Admin"            },
            };

            foreach (var u in testUsers)
            {
                // Skip if user already exists
                var existingUser = await userManager.FindByEmailAsync(u.Email);
                if (existingUser != null)
                {
                    // Make sure existing user has the correct role
                    if (!await userManager.IsInRoleAsync(existingUser, u.Role))
                        await userManager.AddToRoleAsync(existingUser, u.Role);
                    continue;
                }

                var user = new ApplicationUser
                {
                    UserName = u.Email,
                    Email = u.Email,
                    FullName = u.FullName,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    var roleResult = await userManager.AddToRoleAsync(user, u.Role);
                    if (!roleResult.Succeeded)
                    {
                        throw new Exception($"Failed to assign role '{u.Role}' to user '{u.Email}': " +
                            string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                    }
                }
                else
                {
                    throw new Exception($"Failed to create user '{u.Email}': " +
                        string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }
    }
}