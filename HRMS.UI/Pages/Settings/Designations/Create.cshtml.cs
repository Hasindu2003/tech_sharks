using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HRMS.Application.Designations.Commands;
using HRMS.Application.Common;
using HRMS.Application.Entity.Commands;

namespace HRMS.UI.Pages.Settings.Designations
{
    [Authorize(Roles = "Admin")]
    public class CreateModel : PageModel
    {
        private readonly ICommandHandler<CreateDesignationCommand, Result> _handler;

        public CreateModel(ICommandHandler<CreateDesignationCommand, Result> handler)
        {
            _handler = handler;
        }

        [BindProperty]
        public CreateDesignationCommand NewDesignation { get; set; } = new();

        public IActionResult OnGet() => Page();

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var result = await _handler.HandleAsync(NewDesignation);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("NewDesignation.Title", result.Error!);
                return Page();
            }

            TempData["SuccessMessage"] = $"Designation '{NewDesignation.Title}' created. You can now assign it to departments.";
            return RedirectToPage("./Index");
        }
    }
}
