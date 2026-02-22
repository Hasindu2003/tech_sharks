using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Persistence;

namespace HRMS.UI.Pages.Settings.Designations
{
    [Authorize(Roles = "Admin")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Designation EditingDesignation { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            var desig = await _context.Designations.FirstOrDefaultAsync(m => m.Id == id);
            if (desig == null) return NotFound();

            EditingDesignation = desig;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Attach(EditingDesignation).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Designation '{EditingDesignation.Title}' updated successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DesignationExists(EditingDesignation.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private bool DesignationExists(int id)
        {
            return _context.Designations.Any(e => e.Id == id);
        }
    }
}
