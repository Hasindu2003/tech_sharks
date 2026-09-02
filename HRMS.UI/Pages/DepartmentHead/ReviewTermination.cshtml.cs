using System;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Application.Models;
using HRMS.Application.Services;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.DepartmentHead
{
    [Authorize(Roles = "Department Head")]
    public class ReviewTerminationModel : PageModel
    {
        private readonly ITerminationService _terminationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ReviewTerminationModel(
            ITerminationService terminationService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _terminationService = terminationService;
            _userManager = userManager;
            _context = context;
        }

        public TerminationRequestViewModel? TerminationRequest { get; set; }
        public string DeptHeadBranch { get; set; } = string.Empty;
        public string DeptHeadDepartment { get; set; } = string.Empty;
        public TerminationDepartmentReviewViewModel? MyDepartmentReview { get; set; }
        public bool CanReview { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var user = await ResolveCurrentUserAsync();
            await ResolveBranchAndDepartmentAsync(user);

            TerminationRequest = await _terminationService.GetTerminationByIdAsync(id);
            if (TerminationRequest == null) return NotFound();

            MyDepartmentReview = TerminationRequest.DepartmentReviews
                .FirstOrDefault(dr => MatchDept(dr.DepartmentName, DeptHeadDepartment));

            CanReview = TerminationRequest.Status == TerminationStatusEnum.SubmittedForApproval &&
                        (MyDepartmentReview == null || MyDepartmentReview.Status == "Pending");

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id, string action, string comments)
        {
            if (string.IsNullOrWhiteSpace(comments) || comments.Trim().Length < 5)
            {
                TempData["ErrorMessage"] = "Comments are required (minimum 5 characters).";
                return RedirectToPage(new { id });
            }

            var user = await ResolveCurrentUserAsync();
            if (user == null) return Challenge();

            await ResolveBranchAndDepartmentAsync(user);

            var req = await _terminationService.GetTerminationByIdAsync(id);
            if (req == null) return NotFound();

            bool isApprove = string.Equals(action, "approve", StringComparison.OrdinalIgnoreCase);
            var status = isApprove ? "Approved" : "Rejected";

            var reviewerName = !string.IsNullOrWhiteSpace(user.FullName)
                ? user.FullName
                : (user.UserName ?? user.Email ?? "Department Head");

            var result = await _terminationService.DeptHeadReviewAsync(
                id,
                !string.IsNullOrWhiteSpace(DeptHeadDepartment) ? DeptHeadDepartment : "General",
                status,
                comments.Trim(),
                user.Id,
                reviewerName,
                user.Email ?? ""
            );

            if (result.Success)
            {
                TempData["SuccessMessage"] = isApprove
                    ? $"Termination clearance for {req.EmployeeName} has been approved successfully."
                    : $"Termination clearance for {req.EmployeeName} has been rejected.";

                return RedirectToPage("/DepartmentHead/ReviewTerminations");
            }

            TempData["ErrorMessage"] = result.ErrorMessage ?? "Failed to submit review.";
            return RedirectToPage(new { id });
        }

        private static bool MatchDept(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            var cleanA = a.ToLower().Replace("department", "").Replace("dept", "").Trim();
            var cleanB = b.ToLower().Replace("department", "").Replace("dept", "").Trim();
            return cleanA == cleanB || cleanA.Contains(cleanB) || cleanB.Contains(cleanA);
        }

        private async Task<ApplicationUser?> ResolveCurrentUserAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null) return user;

            var username = User.Identity?.Name;
            if (!string.IsNullOrEmpty(username))
            {
                user = await _userManager.FindByNameAsync(username) ??
                       await _userManager.FindByEmailAsync(username);
            }
            return user;
        }

        private async Task ResolveBranchAndDepartmentAsync(ApplicationUser? user)
        {
            if (user == null) return;

            DeptHeadBranch = user.Branch ?? string.Empty;
            DeptHeadDepartment = user.Department ?? string.Empty;

            if (user.EmployeeId.HasValue && user.EmployeeId.Value > 0)
            {
                var emp = await _context.Employees
                    .Include(e => e.Branch)
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value);

                if (emp != null)
                {
                    if (string.IsNullOrWhiteSpace(DeptHeadBranch) && emp.Branch != null)
                        DeptHeadBranch = emp.Branch.Name;

                    if (string.IsNullOrWhiteSpace(DeptHeadDepartment) && emp.Department != null)
                        DeptHeadDepartment = emp.Department.Name;
                }
            }
        }
    }
}
