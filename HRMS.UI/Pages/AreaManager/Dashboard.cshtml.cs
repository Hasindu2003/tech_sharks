using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.AreaManager
{
    [Authorize(Roles = "Admin,Area Manager")]
    public class DashboardModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}