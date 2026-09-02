using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Resignation
{
    [Authorize]
    public class AcceptanceLetterModel : PageModel
    {
        public IActionResult OnGet(int id)
        {
            return RedirectToPage("/Resignation/Details", new { id });
        }
    }
}
