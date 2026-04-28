using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Persistence;

namespace HRMS.UI.Pages.Employees
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CreateModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Employee NewEmployee { get; set; } = new();

        public SelectList DepartmentList { get; set; } = default!;
        public SelectList DesignationList { get; set; } = default!;
        public SelectList BranchList { get; set; } = default!;

        public async Task OnGetAsync()
        {
            await LoadDropdownsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ModelState.Remove("NewEmployee.Department");
            ModelState.Remove("NewEmployee.Designation");
            ModelState.Remove("NewEmployee.Branch");
            ModelState.Remove("NewEmployee.Status");
            ModelState.Remove("NewEmployee.DateJoined");

            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync();
                return Page();
            }

            NewEmployee.DateJoined = DateTime.Now;
            NewEmployee.Status = "Active";

            _context.Employees.Add(NewEmployee);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Employee {NewEmployee.FirstName} created successfully.";
            return RedirectToPage("./Index");
        }

        private async Task LoadDropdownsAsync()
        {
            var deps = await _context.Departments.ToListAsync();
            var desigs = await _context.Designations.ToListAsync();
            var branches = await _context.Branches.ToListAsync();

            DepartmentList = new SelectList(deps, "Id", "Name");
            DesignationList = new SelectList(desigs, "Id", "Title");
            BranchList = new SelectList(branches, "Id", "Name");
        }
    }
}
