using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HRMS.Application.Departments.Commands;
using HRMS.Application.Common;
using HRMS.Application.Entity.Commands;

namespace HRMS.UI.Pages.Settings.Departments
{
    [Authorize(Roles = "Admin")]
    public class CreateModel : PageModel
    {
        private readonly ICommandHandler<CreateDepartmentCommand, Result> _handler;

        public CreateModel(ICommandHandler<CreateDepartmentCommand, Result> handler)
        {
            _handler = handler;
        }

        [BindProperty]
        public CreateDepartmentCommand NewDepartment { get; set; } = new();

        public IActionResult OnGet() => Page();

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var result = await _handler.HandleAsync(NewDepartment);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("NewDepartment.Name", result.Error!);
                return Page();
            }

            TempData["SuccessMessage"] = $"Department '{NewDepartment.Name}' created. You can now assign it to branches.";
            return RedirectToPage("./Index");
        }
    }
}
