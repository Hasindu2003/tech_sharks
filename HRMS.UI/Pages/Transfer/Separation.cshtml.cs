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
        private readonly ITerminationService _terminationService;
        private readonly IResignationService _resignationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public SeparationModel(ITransferRequestService transferService, ITerminationService terminationService, IResignationService resignationService, UserManager<ApplicationUser> userManager)
        {
            _transferService = transferService;
            _terminationService = terminationService;
            _resignationService = resignationService;
            _userManager = userManager;
        }

        public List<TransferRequestViewModel> MyRequests { get; set; } = new();
        public List<TerminationRequestViewModel> MyTerminations { get; set; } = new();
        public List<ResignationRequestViewModel> MyResignations { get; set; } = new();
        public int TransferCount { get; set; }
        public int TerminationCount { get; set; }
        public int ResignationCount { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ActiveTab { get; set; } = "Transfer";

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                MyRequests = await _transferService.GetRequestsByUserAsync(user.Email!);
                TransferCount = MyRequests.Count;

                MyTerminations = await _terminationService.GetTerminationsByEmployeeEmailAsync(user.Email!);
                TerminationCount = MyTerminations.Count;

                MyResignations = await _resignationService.GetMyResignationsAsync(user.Email!);
                ResignationCount = MyResignations.Count;
            }
        }
    }
}
