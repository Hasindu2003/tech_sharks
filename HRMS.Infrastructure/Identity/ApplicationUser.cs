using Microsoft.AspNetCore.Identity;

namespace HRMS.Infrastructure.Identity
{
    public class ApplicationUser : IdentityUser
    {
        // Full name for display purposes
        public string FullName { get; set; } = string.Empty;
    }
}
