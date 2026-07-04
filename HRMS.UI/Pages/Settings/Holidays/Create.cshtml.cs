using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Settings.Holidays
{
    [Authorize(Roles = "Admin,HR Manager")]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CreateModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty] public Holiday NewHoliday { get; set; } = new();

        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            _context.Holidays.Add(NewHoliday);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"'{NewHoliday.Name}' added.";
            return RedirectToPage("./Index");
        }
    }
}
