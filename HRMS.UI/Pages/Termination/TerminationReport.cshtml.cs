using HRMS.Infrastructure.Identity;
using HRMS.UI.Models;
using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Termination
{
    [Authorize]
    public class TerminationReportModel : PageModel
    {
        private readonly ITerminationService _terminationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public TerminationReportModel(ITerminationService terminationService, UserManager<ApplicationUser> userManager)
        {
            _terminationService = terminationService;
            _userManager = userManager;
        }

        public new TerminationRequestViewModel? Request { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Request = await _terminationService.GetTerminationByIdAsync(id);
            if (Request == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Employees can only view their own report
            // HR Manager and Area Manager can view any report
            if (User.IsInRole("Employee"))
            {
                if (Request.EmployeeEmail != user.Email)
                    return Forbid();
            }

            return Page();
        }
    }
}
