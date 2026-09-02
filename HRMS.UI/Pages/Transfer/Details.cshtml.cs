using HRMS.Application.Models;
using HRMS.Application.Services;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.Transfer
{
    [Authorize]
    public class DetailsModel : PageModel
    {
        private readonly ITransferRequestService _transferService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public DetailsModel(
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
            TransferRequest = await _transferService.GetRequestByIdAsync(id);

            if (TransferRequest == null)
                return NotFound();

            if (User.IsInRole("HR Manager") ||
                User.IsInRole("HR Officer") ||
                User.IsInRole("Area Manager") ||
                User.IsInRole("Branch Manager") ||
                User.IsInRole("Department Head"))
            {
                return Page();
            }

            var username = User.Identity?.Name;
            var user = await _userManager.GetUserAsync(User);
            if (user == null && !string.IsNullOrEmpty(username))
            {
                user = await _userManager.FindByNameAsync(username) ?? await _userManager.FindByEmailAsync(username);
            }

            HRMS.Domain.Entities.Core.Employee? employee = null;
            if (user?.EmployeeId.HasValue == true)
            {
                employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value);
            }
            if (employee == null && !string.IsNullOrEmpty(user?.Email))
            {
                employee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == user.Email);
            }
            if (employee == null && !string.IsNullOrEmpty(username))
            {
                employee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == username);
            }

            var idSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(username)) idSet.Add(username);
            if (!string.IsNullOrEmpty(user?.UserName)) idSet.Add(user.UserName);
            if (!string.IsNullOrEmpty(user?.Email)) idSet.Add(user.Email);
            if (!string.IsNullOrEmpty(employee?.Email)) idSet.Add(employee.Email);

            var epf = employee?.EPFNumber ?? user?.EpfNumber;
            var fullName = employee?.FullName ?? user?.FullName;

            bool isOwner = (!string.IsNullOrEmpty(TransferRequest.RequestedBy) && idSet.Contains(TransferRequest.RequestedBy))
                        || (!string.IsNullOrEmpty(TransferRequest.EmployeeEmail) && idSet.Contains(TransferRequest.EmployeeEmail))
                        || (!string.IsNullOrEmpty(epf) && string.Equals(TransferRequest.EpfNumber, epf, StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrEmpty(fullName) && string.Equals(TransferRequest.EmployeeName, fullName, StringComparison.OrdinalIgnoreCase));

            if (!isOwner)
            {
                return Forbid();
            }

            return Page();
        }
    }
}