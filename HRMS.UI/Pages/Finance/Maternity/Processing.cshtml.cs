using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Persistence;
using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Finance.Maternity
{
    [Authorize(Roles = "HR Manager")]
    public class ProcessingModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IMaternityLeaveService _maternityService;

        public ProcessingModel(ApplicationDbContext context, IMaternityLeaveService maternityService)
        {
            _context = context;
            _maternityService = maternityService;
        }

        public List<Leave> ApprovedLeaves { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            ApprovedLeaves = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.MaternityPayment)
                .Where(l => l.LeaveType == "Maternity" && l.Status == "Approved")
                .Where(l => l.MaternityPayment == null || l.MaternityPayment.Status != "Processed")
                .OrderByDescending(l => l.ApprovedDate)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostProcessAsync(int leaveId, string salaryType, decimal percentage, string nursingConfig)
        {
            try
            {
                if (string.Equals(salaryType, "Full", System.StringComparison.OrdinalIgnoreCase))
                {
                    percentage = 100m;
                }
                else if (string.Equals(salaryType, "NoPay", System.StringComparison.OrdinalIgnoreCase))
                {
                    percentage = 0m;
                }
                else if (string.Equals(salaryType, "Half", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (percentage <= 0m || percentage >= 100m)
                    {
                        percentage = 50m;
                    }
                }

                await _maternityService.ProcessMaternityPayrollAsync(leaveId, salaryType, percentage, nursingConfig);
                SuccessMessage = "Maternity payroll processed successfully!";
            }
            catch (System.Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            return RedirectToPage();
        }
    }
}
