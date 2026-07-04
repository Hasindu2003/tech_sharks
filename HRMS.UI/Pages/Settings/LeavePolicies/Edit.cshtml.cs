using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Settings.LeavePolicies
{
    [Authorize(Roles = "Admin,HR Manager")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty] public LeavePolicy EditingPolicy { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            var policy = await _context.LeavePolicies.FirstOrDefaultAsync(p => p.Id == id);
            if (policy == null) return NotFound();

            EditingPolicy = policy;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            _context.Attach(EditingPolicy).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{EditingPolicy.Name} policy updated.";
            return RedirectToPage("./Index");
        }
    }
}
