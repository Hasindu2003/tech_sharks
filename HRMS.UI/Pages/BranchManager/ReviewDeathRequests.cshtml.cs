using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace HRMS.UI.Pages.BranchManager
{
    [Authorize(Roles = "Admin,Branch Manager")]
    public class ReviewDeathRequestsModel : PageModel
    {
        private readonly IDeathService _deathService;

        public ReviewDeathRequestsModel(IDeathService deathService)
        {
            _deathService = deathService;
        }

        public List<DeathRequestViewModel> Requests { get; set; } = new();

        public async Task OnGetAsync()
        {
            var branch = User.FindFirstValue("Branch") ?? "";
            Requests = await _deathService.GetAllPendingForBMAsync(branch);
        }
    }
}
