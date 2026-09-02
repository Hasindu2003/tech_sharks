using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Training
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

            // Employees only have Training Sessions, so redirect them directly
            bool isManagement = User.IsInRole("HR Manager") || User.IsInRole("HR Officer") || 
                                User.IsInRole("Branch Manager") || User.IsInRole("Area Manager") || 
                                User.IsInRole("Department Head");

            if (!isManagement)
            {
                return RedirectToPage("/Training/Sessions");
            }

            return Page();
        }
    }
}
