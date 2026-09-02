using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.DepartmentHead
{
    [Authorize(Roles = "Department Head")]
    public class ReviewTransfersModel : PageModel
    {
        private readonly ITransferRequestService _transferService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ReviewTransfersModel(
            ITransferRequestService transferService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _transferService = transferService;
            _userManager = userManager;
            _context = context;
        }

        public List<TransferRequestViewModel> PendingRequests { get; set; } = new();
        public List<TransferRequestViewModel> ReviewedRequests { get; set; } = new();
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

            PendingRequests  = await _transferService.GetRequestsForDeptHeadAsync(DeptHeadBranch, DeptHeadDepartment);
            ReviewedRequests = await _transferService.GetReviewedByDeptHeadAsync(DeptHeadBranch, DeptHeadDepartment);
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
    }
}
