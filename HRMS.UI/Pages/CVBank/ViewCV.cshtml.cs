using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IO;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.CVBank
{
    [Authorize(Roles = "HR Manager, HR Officer, Area Manager, Branch Manager")]
    public class ViewCVModel : PageModel
    {
        private readonly ICVBankService _cvService;
        private readonly IWebHostEnvironment _environment;

        public ViewCVModel(ICVBankService cvService, IWebHostEnvironment environment)
        {
            _cvService = cvService;
            _environment = environment;
        }

        public HRMS.Domain.Entities.CVBank Candidate { get; set; } = new HRMS.Domain.Entities.CVBank();
        public bool HasValidFile { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var cvEntry = await _cvService.GetCVByIdAsync(id);

            if (cvEntry == null)
            {
                return NotFound();
            }

            Candidate = cvEntry;

            if (!string.IsNullOrEmpty(Candidate.CVFilePath))
            {
                var fullPath = Path.Combine(_environment.WebRootPath, Candidate.CVFilePath.TrimStart('/').TrimStart('\\'));
                HasValidFile = System.IO.File.Exists(fullPath);
            }

            return Page();
        }
    }
}
