using Microsoft.AspNetCore.Identity;

namespace HRMS.Infrastructure.Identity
{
    public class ApplicationUser : IdentityUser
    {
        // Link to the Employee record (optional)
        public int? EmployeeId { get; set; }

        // Forces a redirect to the change-password page until the user sets their own password.
        public bool MustChangePassword { get; set; } = true;
    }
}
