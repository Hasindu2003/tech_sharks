using System;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Documents
{
    [Authorize]
    public class ViewerModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        [FromQuery(Name = "url")]
        public string? FileUrl { get; set; }

        [BindProperty(SupportsGet = true)]
        [FromQuery(Name = "path")]
        public string? PathUrl { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Title { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Name { get; set; }

        public string DocUrl { get; set; } = string.Empty;
        public string DocTitle { get; set; } = "Document Viewer";
        public string FileName { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public bool IsImage { get; set; }
        public bool IsPdf { get; set; }

        public IActionResult OnGet()
        {
            var rawUrl = !string.IsNullOrWhiteSpace(FileUrl) ? FileUrl : PathUrl;
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                return RedirectToPage("/Index");
            }

            var cleanUrl = rawUrl.Trim();
            if (!cleanUrl.StartsWith("/") && !cleanUrl.StartsWith("~"))
            {
                return BadRequest("Invalid document URL.");
            }

            DocUrl = cleanUrl;
            FileName = !string.IsNullOrWhiteSpace(Name) 
                ? Name 
                : Path.GetFileName(cleanUrl.Split('?')[0]);

            DocTitle = !string.IsNullOrWhiteSpace(Title) 
                ? Title 
                : (!string.IsNullOrWhiteSpace(FileName) ? FileName : "Document Viewer");

            FileExtension = Path.GetExtension(cleanUrl.Split('?')[0]).ToLowerInvariant();

            IsImage = FileExtension is ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".bmp" or ".svg";
            IsPdf = FileExtension is ".pdf";

            return Page();
        }
    }
}
