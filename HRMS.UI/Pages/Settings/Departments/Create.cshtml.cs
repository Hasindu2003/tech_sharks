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
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CreateModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public Department NewDepartment { get; set; } = new();

        public SelectList BranchList { get; set; } = default!;
        public bool IsAdmin { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            await SetupPageDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await SetupPageDataAsync();

            ModelState.Remove("NewDepartment.Branch");
            ModelState.Remove("NewDepartment.Employees");

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            
            if (!IsAdmin)
            {
                // HR Manager forces their own branch
                if (user?.EmployeeId != null)
                {
                    var emp = await _context.Employees.FindAsync(user.EmployeeId);
                    if (emp != null)
                    {
                        NewDepartment.BranchId = emp.BranchId;
                    }
                    else
                    {
                        ModelState.AddModelError("", "Your HR profile does not have an assigned branch. Cannot create department.");
                        return Page();
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Your account is not linked to an HR profile. Cannot create department.");
                    return Page();
                }
            }

            _context.Departments.Add(NewDepartment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Department '{NewDepartment.Name}' created successfully.";
            return RedirectToPage("./Index");
        }

        private async Task SetupPageDataAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            IsAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (IsAdmin)
            {
                var branches = await _context.Branches.ToListAsync();
                BranchList = new SelectList(branches, "Id", "Name");
            }
        }
    }
}
