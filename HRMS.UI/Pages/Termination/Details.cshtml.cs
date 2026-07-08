using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Termination
{
    [Authorize(Roles = "HR Manager,Area Manager,Branch Manager")]
    public class DetailsModel : PageModel
    {
        private readonly ITerminationService _terminationService;

        public DetailsModel(ITerminationService terminationService)
        {
            _terminationService = terminationService;
        }

        public new TerminationRequestViewModel? Request { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Request = await _terminationService.GetTerminationByIdAsync(id);
            if (Request == null) return NotFound();
            return Page();
        }
    }
}
