using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.HRManager
{
    [Authorize(Roles = "HR Manager,HR Officer")]
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
            Requests = await _deathService.GetAllPendingForHRAsync();
        }
    }
}
