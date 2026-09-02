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
    public class CreateModel : PageModel
    {
        private readonly ICommandHandler<CreateDesignationCommand, Result> _handler;
        private readonly ApplicationDbContext _context;

        public CreateModel(ICommandHandler<CreateDesignationCommand, Result> handler, ApplicationDbContext context)
        {
            _handler = handler;
            _context = context;
        }

        [BindProperty]
        public CreateDesignationCommand NewDesignation { get; set; } = new();

        public SelectList DepartmentList { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadDepartmentsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (NewDesignation.DepartmentId <= 0)
            {
                ModelState.AddModelError("NewDesignation.DepartmentId", "Please select a department.");
            }

            if (!ModelState.IsValid)
            {
                await LoadDepartmentsAsync();
                return Page();
            }

            var result = await _handler.HandleAsync(NewDesignation);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("NewDesignation.Title", result.Error!);
                await LoadDepartmentsAsync();
                return Page();
            }

            TempData["SuccessMessage"] = $"Designation '{NewDesignation.Title}' created successfully.";
            return RedirectToPage("./Index");
        }

        private async Task LoadDepartmentsAsync()
        {
            var depts = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            DepartmentList = new SelectList(depts, "Id", "Name");
        }
    }
}
