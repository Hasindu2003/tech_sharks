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
            optionsBuilder.UseMySql(
                "Server=localhost;Port=3306;Database=hrms_db_2;User=root;Password=;",
                new MySqlServerVersion(new Version(8, 0, 0)));

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
