using HRMS.Infrastructure.Identity;
using HRMS.UI.Models;
using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Resignation
{
    [Authorize]
    public class DetailsModel : PageModel
    {
        private readonly IResignationService _resignationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public DetailsModel(IResignationService resignationService, UserManager<ApplicationUser> userManager)
        {
            _resignationService = resignationService;
            _userManager = userManager;
        }

        public new ResignationRequestViewModel? Request { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Request = await _resignationService.GetByIdAsync(id);
            if (Request == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Employees can only view their own
            if (User.IsInRole("Employee") && Request.EmployeeEmail != user.Email)
                return Forbid();

            return Page();
        }
    }
}
