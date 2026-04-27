using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public IActionResult OnGet()
        {
            // If already logged in, redirect to their dashboard by role
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Employee"))
                    return RedirectToPage("/Welfare/RequestList");

                if (User.IsInRole("BranchDGM"))
                    return RedirectToPage("/Welfare/Approvals/BranchDGMApproval");

                if (User.IsInRole("HODGM"))
                    return RedirectToPage("/Welfare/Approvals/HODGMApproval");

                if (User.IsInRole("SeniorManagement"))
                    return RedirectToPage("/Welfare/Approvals/SeniorManagementApproval");

                if (User.IsInRole("Finance"))
                    return RedirectToPage("/Welfare/Approvals/FinanceApproval");

                if (User.IsInRole("Admin"))
                    return RedirectToPage("/Welfare/RequestList");

                return RedirectToPage("/Account/Login");
            }

            // Not logged in — go to the real login page
            return RedirectToPage("/Account/Login");
        }
    }
}
