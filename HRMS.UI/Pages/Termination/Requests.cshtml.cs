using HRMS.UI.Models;
using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Termination
{
    [Authorize(Roles = "HR Manager,Area Manager")]
    public class RequestsModel : PageModel
    {
        private readonly ITerminationService _terminationService;

        public RequestsModel(ITerminationService terminationService)
        {
            _terminationService = terminationService;
        }

        public List<TerminationRequestViewModel> Requests { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        public async Task OnGetAsync()
        {
            Requests = await _terminationService.GetTerminationRequestsAsync(StatusFilter, Search);
        }
    }
}
