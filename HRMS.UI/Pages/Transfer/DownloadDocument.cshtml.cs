using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Transfer
{
    [Authorize]
    public class DownloadDocumentModel : PageModel
    {
        private readonly ITransferRequestService _transferService;

        public DownloadDocumentModel(ITransferRequestService transferService)
        {
            _transferService = transferService;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var (data, fileName, contentType) = await _transferService.GetDocumentAsync(id);

            if (data == null || fileName == null)
                return NotFound("Document not found.");

            return File(data, contentType ?? "application/octet-stream", fileName);
        }
    }
}