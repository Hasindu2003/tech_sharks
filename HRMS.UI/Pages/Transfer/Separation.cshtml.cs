using HRMS.Infrastructure.Identity;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Transfer
{
    [Authorize(Roles = "Employee")]
    public class SeparationModel : PageModel
    {
        private readonly ITransferRequestService _transferService;
        private readonly IResignationService _resignationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public SeparationModel(
            ITransferRequestService transferService,
            IResignationService resignationService,
            UserManager<ApplicationUser> userManager)
        {
            _transferService = transferService;
            _resignationService = resignationService;
            _userManager = userManager;
        }

        public List<TransferRequestViewModel> MyRequests { get; set; } = new();
        public List<ResignationRequestViewModel> MyResignations { get; set; } = new();
        public int TransferCount { get; set; }
        public int ResignationCount { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ActiveTab { get; set; } = "Transfer";

        public async Task OnGetAsync()
        {
            var username = User.Identity?.Name;
            var user = await _userManager.GetUserAsync(User);
            if (user == null && !string.IsNullOrEmpty(username))
            {
                user = await _userManager.FindByNameAsync(username) ?? await _userManager.FindByEmailAsync(username);
            }

            var identifier = user?.Email ?? user?.UserName ?? username ?? "";
            if (!string.IsNullOrEmpty(identifier))
            {
                MyRequests = await _transferService.GetRequestsByUserAsync(identifier);
                TransferCount = MyRequests.Count;

                MyResignations = await _resignationService.GetMyResignationsAsync(identifier);
                ResignationCount = MyResignations.Count;
            }
        }

        public async Task<IActionResult> OnPostDeleteDraftAsync(int id)
        {
            var username = User.Identity?.Name;
            var user = await _userManager.GetUserAsync(User);
            if (user == null && !string.IsNullOrEmpty(username))
            {
                user = await _userManager.FindByNameAsync(username) ?? await _userManager.FindByEmailAsync(username);
            }

            var identifier = user?.Email ?? user?.UserName ?? username ?? "";
            if (string.IsNullOrEmpty(identifier))
            {
                return Challenge();
            }

            (bool success, string? error) = await _resignationService.DeleteDraftAsync(id, identifier);
            if (!success)
            {
                TempData["ErrorMessage"] = error;
            }
            else
            {
                TempData["SuccessMessage"] = "Resignation draft has been deleted successfully.";
            }

            return RedirectToPage(new { ActiveTab = "Resignation" });
        }
    }
}
