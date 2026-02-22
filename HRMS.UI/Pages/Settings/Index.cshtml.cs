using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Settings
{
    [Authorize(Roles = "Admin,HR Manager")]
    public class IndexModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
