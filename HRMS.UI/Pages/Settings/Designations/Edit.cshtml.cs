using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
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

        public SelectList DepartmentList { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            var desig = await _context.Designations.FindAsync(id);
            if (desig == null) return NotFound();

            var currentDD = await _context.DepartmentDesignations
                .FirstOrDefaultAsync(dd => dd.DesignationId == id);

            EditingDesignation = new EditDesignationCommand
            {
                Id           = desig.Id,
                Title        = desig.Title,
                DepartmentId = currentDD?.DepartmentId ?? 0
            };

            await LoadDepartmentsAsync(EditingDesignation.DepartmentId);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (EditingDesignation.DepartmentId <= 0)
            {
                ModelState.AddModelError("EditingDesignation.DepartmentId", "Please select a department.");
            }

            if (!ModelState.IsValid)
            {
                await LoadDepartmentsAsync(EditingDesignation.DepartmentId);
                return Page();
            }

            var result = await _handler.HandleAsync(EditingDesignation);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("EditingDesignation.Title", result.Error!);
                await LoadDepartmentsAsync(EditingDesignation.DepartmentId);
                return Page();
            }

            TempData["SuccessMessage"] = $"Designation '{EditingDesignation.Title}' updated successfully.";
            return RedirectToPage("./Index");
        }

        private async Task LoadDepartmentsAsync(int selectedDeptId = 0)
        {
            var depts = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            DepartmentList = new SelectList(depts, "Id", "Name", selectedDeptId);
        }
    }
}
