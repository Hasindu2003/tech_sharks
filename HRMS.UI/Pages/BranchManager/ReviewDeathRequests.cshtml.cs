using HRMS.Application.Models;
using HRMS.Application.Services;
using HRMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.BranchManager
{
    [Authorize(Roles = "Branch Manager")]
    public class ReviewDeathRequestsModel : PageModel
    {
        private readonly IDeathService _deathService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewDeathRequestsModel(IDeathService deathService, UserManager<ApplicationUser> userManager)
        {
            _deathService = deathService;
            _userManager = userManager;
        }

        public List<DeathRequestViewModel> Requests { get; set; } = new();

        public async Task OnGetAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var branch = currentUser?.Branch ?? "";
            Requests = await _deathService.GetAllPendingForBMAsync(branch);
        }
    }
}
