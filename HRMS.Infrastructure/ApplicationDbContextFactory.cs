using HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HRMS.Infrastructure
{
    /// <summary>
    /// Allows EF Core CLI tools (migrations) to create the DbContext
    /// without needing a running MySQL server.
    /// </summary>
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

            // Use a placeholder connection string — EF CLI only needs it to build the model,
            // not to actually connect for migration generation.
            optionsBuilder.UseSqlServer(
                "Server=tcp:hrmskanrich.database.windows.net,1433;Initial Catalog=HRMS;Persist Security Info=False;User ID=hrmsadmin;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
