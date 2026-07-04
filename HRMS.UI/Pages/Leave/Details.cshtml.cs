using HRMS.Application.Leave;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Leave
{
    [Authorize]
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILeaveService _leaveService;
        private readonly UserManager<ApplicationUser> _userManager;

        public DetailsModel(ILeaveService leaveService, UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _leaveService = leaveService;
            _userManager = userManager;
            _context = context;
        }

        public LeaveDetailsDto? Details { get; set; }
        public bool IsOwner { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Details = await _leaveService.GetDetailsAsync(id);
            if (Details == null)
                return NotFound();

            var user = await _userManager.GetUserAsync(User);
            IsOwner = user?.EmployeeId == Details.Leave.EmployeeId;

            if (!IsOwner && !User.IsInRole("Admin") && !User.IsInRole("HR Manager"))
            {
                var isManagerOfRequester = user?.EmployeeId != null &&
                                           await _context.Employees.AnyAsync(e =>
                                               e.Id == Details.Leave.EmployeeId && e.ManagerId == user.EmployeeId);

                if (!isManagerOfRequester)
                    return Forbid();
            }

            return Page();
        }
    }
}
