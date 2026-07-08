using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HRMS.Application.Branches.Commands;
using HRMS.Application.Common;
using HRMS.Application.Entity.Commands;

namespace HRMS.UI.Pages.Settings.Branches
{
    [Authorize(Roles = "Admin")]
    public class CreateModel : PageModel
    {
        private readonly ICommandHandler<CreateBranchCommand, Result> _handler;

        public CreateModel(ICommandHandler<CreateBranchCommand, Result> handler)
        {
            _handler = handler;
        }

        [BindProperty]
        public CreateBranchCommand NewBranch { get; set; } = new();

        public IActionResult OnGet() => Page();

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var result = await _handler.HandleAsync(NewBranch);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("NewBranch.Name", result.Error!);
                return Page();
            }

            TempData["SuccessMessage"] = $"Branch '{NewBranch.Name}' created successfully.";
            return RedirectToPage("./Index");
        }
    }
}
