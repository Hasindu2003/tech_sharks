using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Separation
{
    [Authorize(Roles = "Branch Manager,Area Manager,HR Manager")]
    public class DashboardModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
