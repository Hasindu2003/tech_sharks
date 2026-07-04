using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Settings.Holidays
{
    [Authorize(Roles = "Admin,HR Manager")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty] public Holiday EditingHoliday { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            var holiday = await _context.Holidays.FirstOrDefaultAsync(h => h.Id == id);
            if (holiday == null) return NotFound();

            EditingHoliday = holiday;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            _context.Attach(EditingHoliday).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"'{EditingHoliday.Name}' updated.";
            return RedirectToPage("./Index");
        }
    }
}
