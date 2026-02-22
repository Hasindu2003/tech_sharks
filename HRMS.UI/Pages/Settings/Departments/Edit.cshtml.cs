using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;

namespace HRMS.UI.Pages.Settings.Departments
{
    [Authorize(Roles = "Admin,HR Manager")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EditModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public Department EditingDepartment { get; set; } = default!;

        public SelectList BranchList { get; set; } = default!;
        public bool IsAdmin { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            var department = await _context.Departments.FirstOrDefaultAsync(m => m.Id == id);
            if (department == null) return NotFound();

            EditingDepartment = department;

            if (!await SetupPageDataAndValidateAccessAsync(department.BranchId))
            {
                return Forbid();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var originalDept = await _context.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == EditingDepartment.Id);
            if (originalDept == null) return NotFound();

            if (!await SetupPageDataAndValidateAccessAsync(originalDept.BranchId))
            {
                return Forbid();
            }

            ModelState.Remove("EditingDepartment.Branch");
            ModelState.Remove("EditingDepartment.Employees");

            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (!IsAdmin)
            {
                // HR Manager cannot change the branch ID to something else
                EditingDepartment.BranchId = originalDept.BranchId;
            }

            _context.Attach(EditingDepartment).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Department '{EditingDepartment.Name}' updated successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DepartmentExists(EditingDepartment.Id))
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

        private bool DepartmentExists(int id)
        {
            return _context.Departments.Any(e => e.Id == id);
        }

        private async Task<bool> SetupPageDataAndValidateAccessAsync(int targetBranchId)
        {
            var user = await _userManager.GetUserAsync(User);
            IsAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (IsAdmin)
            {
                var branches = await _context.Branches.ToListAsync();
                BranchList = new SelectList(branches, "Id", "Name");
                return true;
            }

            // For HR Manager, validate that the target branch belongs to them
            if (user?.EmployeeId != null)
            {
                var emp = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == user.EmployeeId);
                if (emp != null && emp.BranchId == targetBranchId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
