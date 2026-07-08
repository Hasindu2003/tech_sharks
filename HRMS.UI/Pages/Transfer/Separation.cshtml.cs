using HRMS.Infrastructure.Identity;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Transfer
{
    [Authorize(Roles = "HR Manager,Area Manager,Branch Manager,Employee")]
    public class SeparationModel : PageModel
    {
        private readonly ITransferRequestService _transferService;
        private readonly ITerminationService _terminationService;
        private readonly IResignationService _resignationService;
        private readonly IDeathService _deathService;
        private readonly UserManager<ApplicationUser> _userManager;

        public SeparationModel(
            ITransferRequestService transferService, 
            ITerminationService terminationService, 
            IResignationService resignationService,
            IDeathService deathService,
            UserManager<ApplicationUser> userManager)
        {
            _transferService = transferService;
            _terminationService = terminationService;
            _resignationService = resignationService;
            _deathService = deathService;
            _userManager = userManager;
        }

        // Employee specific
        public List<TransferRequestViewModel> MyRequests { get; set; } = new();
        public List<TerminationRequestViewModel> MyTerminations { get; set; } = new();
        public List<ResignationRequestViewModel> MyResignations { get; set; } = new();
        public int TransferCount { get; set; }
        public int TerminationCount { get; set; }
        public int ResignationCount { get; set; }

        // Manager specific
        public bool IsManager { get; set; }
        public int PendingTransfersCount { get; set; }
        public int PendingResignationsCount { get; set; }
        public int PendingTerminationsCount { get; set; }
        public int PendingDeathRequestsCount { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ActiveTab { get; set; } = "Transfer";

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return;

            // 1. Fetch personal history
            MyRequests = await _transferService.GetRequestsByUserAsync(user.Email!);
            TransferCount = MyRequests.Count;

            MyTerminations = await _terminationService.GetTerminationsByEmployeeEmailAsync(user.Email!);
            TerminationCount = MyTerminations.Count;

            MyResignations = await _resignationService.GetMyResignationsAsync(user.Email!);
            ResignationCount = MyResignations.Count;

            // 2. Fetch management data if applicable
            IsManager = User.IsInRole("Admin") || User.IsInRole("HR Manager") || User.IsInRole("Area Manager") || User.IsInRole("Branch Manager");
            
            if (IsManager)
            {
                if (User.IsInRole("Admin") || User.IsInRole("HR Manager"))
                {
                    PendingTransfersCount = (await _transferService.GetPendingRequestsForHRManagerAsync()).Count;
                    PendingResignationsCount = (await _resignationService.GetPendingForHRManagerAsync()).Count;
                    PendingTerminationsCount = (await _terminationService.GetPendingApprovalsAsync()).Count;
                    PendingDeathRequestsCount = (await _deathService.GetAllPendingForHRAsync()).Count;
                }
                else if (User.IsInRole("Area Manager"))
                {
                    PendingTransfersCount = (await _transferService.GetRequestsForAreaManagerAsync()).Count;
                    PendingResignationsCount = (await _resignationService.GetPendingForAreaManagerAsync()).Count;
                    PendingDeathRequestsCount = (await _deathService.GetAllPendingForAMAsync()).Count;
                }
                else if (User.IsInRole("Branch Manager"))
                {
                    var branch = user.Branch;
                    PendingTransfersCount = (await _transferService.GetPendingRequestsForBranchManagerAsync(branch)).Count;
                    PendingResignationsCount = (await _resignationService.GetPendingForBranchManagerAsync(branch)).Count;
                    PendingTerminationsCount = (await _terminationService.GetPendingApprovalsAsync(branch)).Count;
                    PendingDeathRequestsCount = (await _deathService.GetAllPendingForBMAsync(branch)).Count;
                }
            }
        }
    }
}
