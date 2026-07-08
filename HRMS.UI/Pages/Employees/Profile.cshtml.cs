using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Employees
{
    [Authorize(Roles = "Employee,Branch Manager,Area Manager")]
    public class ProfileModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}