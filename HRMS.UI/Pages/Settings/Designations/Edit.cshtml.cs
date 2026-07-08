using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HRMS.Application.Designations.Commands;
using HRMS.Application.Common;
using HRMS.Application.Entity.Commands;
using HRMS.Infrastructure.Persistence;

namespace HRMS.UI.Pages.Settings.Designations
{
    [Authorize(Roles = "Admin")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICommandHandler<EditDesignationCommand, Result> _handler;

        public EditModel(ApplicationDbContext context, ICommandHandler<EditDesignationCommand, Result> handler)
        {
            _context = context;
            _handler = handler;
        }

        [BindProperty]
        public EditDesignationCommand EditingDesignation { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            var desig = await _context.Designations.FindAsync(id);
            if (desig == null) return NotFound();

            EditingDesignation = new EditDesignationCommand
            {
                Id = desig.Id,
                Title = desig.Title
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var result = await _handler.HandleAsync(EditingDesignation);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("EditingDesignation.Title", result.Error!);
                return Page();
            }

            TempData["SuccessMessage"] = $"Designation '{EditingDesignation.Title}' updated successfully.";
            return RedirectToPage("./Index");
        }
    }
}
