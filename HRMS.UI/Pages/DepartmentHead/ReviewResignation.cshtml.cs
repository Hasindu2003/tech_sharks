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
    public class ReviewResignationModel : PageModel
    {
        private readonly IResignationService _resignationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ReviewResignationModel(
            IResignationService resignationService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _resignationService = resignationService;
            _userManager = userManager;
            _context = context;
        }

        public ResignationRequestViewModel? ResignationRequest { get; set; }
        public string DeptHeadBranch { get; set; } = string.Empty;
        public string DeptHeadDepartment { get; set; } = string.Empty;
        public ResignationDepartmentReviewViewModel? MyDepartmentReview { get; set; }
        public bool CanReview { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var user = await ResolveCurrentUserAsync();
            await ResolveBranchAndDepartmentAsync(user);

            ResignationRequest = await _resignationService.GetByIdAsync(id);
            if (ResignationRequest == null) return NotFound();

            if (ResignationRequest.IsManagerialNotification)
            {
                TempData["ErrorMessage"] = "Managerial resignation notices are handled directly by the HR Manager.";
                return RedirectToPage("/Separation/Dashboard", new { ActiveTab = "Resignations" });
            }

            // Ensure Dept Head belongs to this resignation's branch
            if (!string.Equals(ResignationRequest.Branch?.Trim(), DeptHeadBranch?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }

            MyDepartmentReview = ResignationRequest.DepartmentReviews
                .FirstOrDefault(dr => dr.DepartmentName.Equals(DeptHeadDepartment, StringComparison.OrdinalIgnoreCase));

            CanReview = ResignationRequest.Status == ResignationStatusEnum.SubmittedForApproval &&
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

            var req = await _resignationService.GetByIdAsync(id);
            if (req == null) return NotFound();

            if (req.IsManagerialNotification)
            {
                TempData["ErrorMessage"] = "Managerial resignation notices are handled directly by the HR Manager.";
                return RedirectToPage("/Separation/Dashboard", new { ActiveTab = "Resignations" });
            }

            if (!string.Equals(req.Branch?.Trim(), DeptHeadBranch?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            bool isApprove = string.Equals(action, "approve", StringComparison.OrdinalIgnoreCase);

            var reviewerName = !string.IsNullOrWhiteSpace(user.FullName)
                ? user.FullName
                : (user.UserName ?? user.Email ?? "Department Head");

            var success = await _resignationService.DeptHeadReviewAsync(
                id,
                DeptHeadDepartment,
                isApprove,
                comments,
                user.Email ?? "",
                reviewerName,
                user.Id
            );

            if (success)
            {
                TempData["SuccessMessage"] = isApprove
                    ? $"Resignation successfully approved for {DeptHeadDepartment} department."
                    : $"Resignation has been rejected by {DeptHeadDepartment} department.";
            }
            else
            {
                TempData["ErrorMessage"] = "Unable to submit your review for this request.";
            }

            return RedirectToPage("/DepartmentHead/ReviewResignations");
        }

        private async Task<ApplicationUser?> ResolveCurrentUserAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null) return user;

            if (!string.IsNullOrWhiteSpace(User.Identity?.Name))
            {
                user = await _userManager.FindByNameAsync(User.Identity.Name);
                if (user != null) return user;
            }

            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
            if (!string.IsNullOrWhiteSpace(emailClaim))
            {
                user = await _userManager.FindByEmailAsync(emailClaim);
            }

            return user;
        }

        private async Task ResolveBranchAndDepartmentAsync(ApplicationUser? user)
        {
            if (user == null) return;

            DeptHeadBranch = user.Branch ?? "";
            DeptHeadDepartment = user.Department ?? "";

            if ((string.IsNullOrWhiteSpace(DeptHeadBranch) || string.IsNullOrWhiteSpace(DeptHeadDepartment)) && user.EmployeeId.HasValue)
            {
                var emp = await _context.Employees
                    .Include(e => e.Branch)
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value);

                if (emp != null)
                {
                    if (string.IsNullOrWhiteSpace(DeptHeadBranch)) DeptHeadBranch = emp.Branch?.Name ?? "";
                    if (string.IsNullOrWhiteSpace(DeptHeadDepartment)) DeptHeadDepartment = emp.Department?.Name ?? "";
                }
            }
        }
    }
}
