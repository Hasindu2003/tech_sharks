using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Persistence;

namespace HRMS.UI.Pages.Settings.Designations
{
    [Authorize(Roles = "Admin")]
    public class AssignModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AssignModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public Designation Designation { get; set; } = default!;
        public List<DepartmentCheckItem> Departments { get; set; } = new();

        [BindProperty]
        public int DesignationId { get; set; }

        [BindProperty]
        public List<int> SelectedDepartmentIds { get; set; } = new();

        public class DepartmentCheckItem
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public bool IsAssigned { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            Designation = await _context.Designations
                .Include(d => d.DepartmentDesignations)
                .FirstOrDefaultAsync(d => d.Id == id) ?? default!;

            if (Designation == null) return NotFound();

            DesignationId = Designation.Id;
            await LoadDepartmentsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var desig = await _context.Designations
                .Include(d => d.DepartmentDesignations)
                .FirstOrDefaultAsync(d => d.Id == DesignationId);

            if (desig == null) return NotFound();

            _context.DepartmentDesignations.RemoveRange(desig.DepartmentDesignations);

            foreach (var deptId in SelectedDepartmentIds)
            {
                _context.DepartmentDesignations.Add(new DepartmentDesignation
                {
                    DepartmentId = deptId,
                    DesignationId = DesignationId
                });
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Department assignments updated for '{desig.Title}'.";
            return RedirectToPage("./Index");
        }

        private async Task LoadDepartmentsAsync()
        {
            var assignedIds = Designation.DepartmentDesignations.Select(dd => dd.DepartmentId).ToHashSet();
            var departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            Departments = departments.Select(d => new DepartmentCheckItem
            {
                Id = d.Id,
                Name = d.Name,
                IsAssigned = assignedIds.Contains(d.Id)
            }).ToList();
        }
    }
}
