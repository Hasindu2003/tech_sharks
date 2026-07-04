using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Employees
{
    [Authorize]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty] public Employee EditingEmployee { get; set; } = default!;

        public SelectList DepartmentList { get; set; } = default!;
        public SelectList DesignationList { get; set; } = default!;
        public SelectList BranchList { get; set; } = default!;
        public SelectList ManagerList { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();

            EditingEmployee = employee;
            await LoadDropdownsAsync(employee.Id);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ModelState.Remove("EditingEmployee.Department");
            ModelState.Remove("EditingEmployee.Designation");
            ModelState.Remove("EditingEmployee.Branch");
            ModelState.Remove("EditingEmployee.Manager");

            if (EditingEmployee.ManagerId == EditingEmployee.Id)
                ModelState.AddModelError("EditingEmployee.ManagerId", "An employee cannot be their own manager.");

            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync(EditingEmployee.Id);
                return Page();
            }

            _context.Attach(EditingEmployee).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Employee {EditingEmployee.FirstName} updated successfully.";
            return RedirectToPage("./Index");
        }

        private async Task LoadDropdownsAsync(int excludeEmployeeId)
        {
            var deps = await _context.Departments.ToListAsync();
            var desigs = await _context.Designations.ToListAsync();
            var branches = await _context.Branches.ToListAsync();
            var employees = await _context.Employees
                .Where(e => e.Id != excludeEmployeeId)
                .OrderBy(e => e.FirstName)
                .ToListAsync();

            DepartmentList = new SelectList(deps, "Id", "Name");
            DesignationList = new SelectList(desigs, "Id", "Title");
            BranchList = new SelectList(branches, "Id", "Name");
            ManagerList = new SelectList(
                employees.Select(e => new { e.Id, Name = $"{e.FirstName} {e.LastName}" }),
                "Id", "Name");
        }
    }
}
