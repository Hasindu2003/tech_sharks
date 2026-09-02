using System;
using System.Collections.Generic;
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
    public class ReviewResignationsModel : PageModel
    {
        private readonly IResignationService _resignationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ReviewResignationsModel(
            IResignationService resignationService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _resignationService = resignationService;
            _userManager = userManager;
            _context = context;
        }

        public List<ResignationRequestViewModel> PendingRequests { get; set; } = new();
        public List<ResignationRequestViewModel> ReviewedRequests { get; set; } = new();
        public string DeptHeadBranch { get; set; } = string.Empty;
        public string DeptHeadDepartment { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            var user = await ResolveCurrentUserAsync();
            if (user != null)
            {
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

            PendingRequests = await _resignationService.GetPendingForDeptHeadAsync(DeptHeadBranch, DeptHeadDepartment);
            ReviewedRequests = await _resignationService.GetReviewedByDeptHeadAsync(DeptHeadBranch, DeptHeadDepartment);
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
    }
}
