using HRMS.UI.Models;
using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Transfer
{
    [Authorize]
    public class DetailsModel : PageModel
    {
        private readonly ITransferRequestService _transferService;

        public DetailsModel(ITransferRequestService transferService)
        {
            _transferService = transferService;
        }

        public TransferRequestViewModel? TransferRequest { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            TransferRequest = await _transferService.GetRequestByIdAsync(id);

            if (TransferRequest == null)
                return NotFound();

            if (!User.IsInRole("Area Manager") &&
                !User.IsInRole("Branch Manager") &&
                TransferRequest.RequestedBy != User.Identity!.Name)
            {
                return Forbid();
            }

            return Page();
        }
    }
}