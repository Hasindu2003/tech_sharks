using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HRMS.Application.Branches.Commands;
using HRMS.Application.Common;
using HRMS.Application.Entity.Commands;
using HRMS.Infrastructure.Persistence;

namespace HRMS.UI.Pages.Settings.Branches
{
    [Authorize(Roles = "Admin")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICommandHandler<EditBranchCommand, Result> _handler;

        public EditModel(ApplicationDbContext context, ICommandHandler<EditBranchCommand, Result> handler)
        {
            _context = context;
            _handler = handler;
        }

        [BindProperty]
        public EditBranchCommand EditingBranch { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();

            EditingBranch = new EditBranchCommand
            {
                Id = branch.Id,
                Name = branch.Name,
                Location = branch.Location
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var result = await _handler.HandleAsync(EditingBranch);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("EditingBranch.Name", result.Error!);
                return Page();
            }

            TempData["SuccessMessage"] = $"Branch '{EditingBranch.Name}' updated successfully.";
            return RedirectToPage("./Index");
        }
    }
}
