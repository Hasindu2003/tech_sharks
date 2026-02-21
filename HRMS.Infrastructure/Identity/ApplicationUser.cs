using Microsoft.AspNetCore.Identity;

namespace HRMS.Infrastructure.Identity
{
    public class ApplicationUser : IdentityUser
    {
        // Link to the Employee record (optional)
        public int? EmployeeId { get; set; }
    }
}
