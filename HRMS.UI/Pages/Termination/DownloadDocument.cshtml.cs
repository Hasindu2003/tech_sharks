using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Termination
{
    [Authorize]
    public class DownloadDocumentModel : PageModel
    {
        private readonly ITerminationService _terminationService;

        public DownloadDocumentModel(ITerminationService terminationService)
        {
            _terminationService = terminationService;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var (data, fileName, contentType) = await _terminationService.GetDocumentAsync(id);
            if (data == null || fileName == null || contentType == null)
                return NotFound();

            return File(data, contentType, fileName);
        }
    }
}
