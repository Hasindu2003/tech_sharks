using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HRMS.Application.Departments.Commands;
using HRMS.Application.Common;
using HRMS.Application.Entity.Commands;
using HRMS.Infrastructure.Persistence;

namespace HRMS.UI.Pages.Settings.Departments
{
    [Authorize(Roles = "Admin")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICommandHandler<EditDepartmentCommand, Result> _handler;

        public EditModel(ApplicationDbContext context, ICommandHandler<EditDepartmentCommand, Result> handler)
        {
            _context = context;
            _handler = handler;
        }

        [BindProperty]
        public EditDepartmentCommand EditingDepartment { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            var dept = await _context.Departments.FindAsync(id);
            if (dept == null) return NotFound();

            EditingDepartment = new EditDepartmentCommand
            {
                Id = dept.Id,
                Name = dept.Name
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var result = await _handler.HandleAsync(EditingDepartment);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("EditingDepartment.Name", result.Error!);
                return Page();
            }

            TempData["SuccessMessage"] = $"Department '{EditingDepartment.Name}' updated successfully.";
            return RedirectToPage("./Index");
        }
    }
}
