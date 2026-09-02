using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Attendance
{
    [Authorize]
    public class DashboardModel : PageModel
    {
        public IActionResult OnGet()
        {
            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            return Page();
        }
    }
}
