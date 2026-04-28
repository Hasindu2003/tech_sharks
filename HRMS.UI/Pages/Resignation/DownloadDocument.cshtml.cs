using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Resignation
{
    [Authorize]
    public class DownloadDocumentModel : PageModel
    {
        private readonly IResignationService _resignationService;

        public DownloadDocumentModel(IResignationService resignationService)
        {
            _resignationService = resignationService;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var (data, fileName, contentType) = await _resignationService.GetDocumentAsync(id);
            if (data == null) return NotFound();
            return File(data, contentType!, fileName!);
        }
    }
}
