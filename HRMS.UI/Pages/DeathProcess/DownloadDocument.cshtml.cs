using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.DeathProcess
{
    [Authorize]
    public class DownloadDocumentModel : PageModel
    {
        private readonly IDeathService _deathService;

        public DownloadDocumentModel(IDeathService deathService)
        {
            _deathService = deathService;
        }

        public async Task<IActionResult> OnGetAsync(int id, string? mode = null)
        {
            var result = await _deathService.DownloadDocumentAsync(id);
            if (result == null) return NotFound();

            var (content, contentType, fileName) = result.Value;
            var effectiveContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;

            if (mode == "view")
            {
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
                return File(content, effectiveContentType);
            }

            return File(content, effectiveContentType, fileName);
        }
    }
}
