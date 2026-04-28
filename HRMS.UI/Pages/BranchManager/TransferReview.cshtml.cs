using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.BranchManager
{
    [Authorize(Roles = "Branch Manager,Area Manager")]
    public class TransferReviewModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}