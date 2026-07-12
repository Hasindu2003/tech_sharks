using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Attendance;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.BiometricLogs
{
    [Authorize(Roles = "Branch Manager, Area Manager, HR Manager")]
    public class HistoryModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        
        public List<BiometricLog> BiometricLogs { get; set; } = new();
        public List<Domain.Entities.Core.Employee> Employees { get; set; } = new();
        
        [BindProperty(SupportsGet = true)]
        public int? EmployeeId { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public string DateFilter { get; set; } = "All";
        
        public HistoryModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task OnGetAsync()
        {
            Employees = await _context.Employees
                .Where(e => e.Status != "Draft" && e.NIC != "DUTY-ACC")
                .OrderBy(e => e.FullName)
                .ToListAsync();
            
            var query = _context.BiometricLogs.Include(b => b.Employee).AsQueryable();
            
            if (EmployeeId.HasValue && EmployeeId.Value > 0)
            {
                query = query.Where(b => b.EmployeeId == EmployeeId.Value);
            }
            
            if (!string.IsNullOrEmpty(DateFilter) && DateFilter != "All")
            {
                var now = DateTime.Now;
                if (DateFilter == "Last7Days")
                {
                    var limit = now.Date.AddDays(-7);
                    query = query.Where(b => b.LogDateTime >= limit);
                }
                else if (DateFilter == "LastMonth")
                {
                    var limit = now.Date.AddMonths(-1);
                    query = query.Where(b => b.LogDateTime >= limit);
                }
                else if (DateFilter == "Last3Months")
                {
                    var limit = now.Date.AddMonths(-3);
                    query = query.Where(b => b.LogDateTime >= limit);
                }
            }
            
            BiometricLogs = await query.OrderByDescending(b => b.LogDateTime).ToListAsync();
        }
    }
}
