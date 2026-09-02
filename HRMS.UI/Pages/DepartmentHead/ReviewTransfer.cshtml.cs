using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.DepartmentHead
{
    [Authorize(Roles = "Department Head")]
    public class ReviewTransferModel : PageModel
    {
        private readonly ITransferRequestService _transferService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ReviewTransferModel(
            ITransferRequestService transferService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _transferService = transferService;
            _userManager = userManager;
            _context = context;
        }

        public TransferRequestViewModel? TransferRequest { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var user = await ResolveCurrentUserAsync();
            var (branch, dept) = await ResolveBranchAndDepartmentAsync(user);

            TransferRequest = await _transferService.GetRequestByIdAsync(id);
            if (TransferRequest == null) return NotFound();

            // Ensure this Dept Head is responsible for this request's branch + department (case-insensitive)
            if (!string.Equals(TransferRequest.CurrentBranch?.Trim(), branch?.Trim(), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(TransferRequest.Department?.Trim(), dept?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id, string action, string comments)
        {
            if (string.IsNullOrWhiteSpace(comments) || comments.Trim().Length < 10)
            {
                ModelState.AddModelError(string.Empty, "Comments must be at least 10 characters.");
                TransferRequest = await _transferService.GetRequestByIdAsync(id);
                return Page();
            }

            var user = await ResolveCurrentUserAsync();
            var (branch, dept) = await ResolveBranchAndDepartmentAsync(user);

            var request = await _transferService.GetRequestByIdAsync(id);
            if (request == null ||
                !string.Equals(request.CurrentBranch?.Trim(), branch?.Trim(), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(request.Department?.Trim(), dept?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }

            bool approved = action == "approve";
            var ok = await _transferService.DeptHeadReviewAsync(id, approved, comments.Trim());
            if (!ok)
            {
                TempData["ErrorMessage"] = "Unable to process review. The request may have already been reviewed.";
                return RedirectToPage("/DepartmentHead/ReviewTransfers");
            }

            TempData["SuccessMessage"] = approved
                ? "Transfer request approved and forwarded to Branch Managers."
                : "Transfer request rejected.";

            return RedirectToPage("/DepartmentHead/ReviewTransfers");
        }

        private async Task<ApplicationUser?> ResolveCurrentUserAsync()
        {
            var username = User.Identity?.Name;
            var user = await _userManager.GetUserAsync(User);
            if (user == null && !string.IsNullOrEmpty(username))
            {
                user = await _userManager.FindByNameAsync(username) ?? await _userManager.FindByEmailAsync(username);
            }
            return user;
        }

        private async Task<(string Branch, string Department)> ResolveBranchAndDepartmentAsync(ApplicationUser? user)
        {
            if (user == null) return (string.Empty, string.Empty);

            var branch = user.Branch ?? "";
            var dept = user.Department ?? "";

            if ((string.IsNullOrWhiteSpace(branch) || string.IsNullOrWhiteSpace(dept)) && user.EmployeeId.HasValue)
            {
                var emp = await _context.Employees
                    .Include(e => e.Branch)
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value);

                if (emp != null)
                {
                    if (string.IsNullOrWhiteSpace(branch)) branch = emp.Branch?.Name ?? "";
                    if (string.IsNullOrWhiteSpace(dept)) dept = emp.Department?.Name ?? "";
                }
            }

            return (branch, dept);
        }
    }
}
