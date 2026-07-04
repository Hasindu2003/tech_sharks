using HRMS.Application.Leave;
using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Application.Payroll
{
    public class PayrollLeaveExportService : IPayrollLeaveExportService
    {
        private readonly ApplicationDbContext _context;

        public PayrollLeaveExportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PayrollLeaveSummaryDto> GetPayrollLeaveSummaryAsync(int employeeId, DateTime periodStart,
            DateTime periodEnd)
        {
            var start = periodStart.Date;
            var end = periodEnd.Date;

            var leaves = await _context.Leaves
                .Where(l => l.EmployeeId == employeeId && l.Status == LeaveStatus.Approved &&
                            l.StartDate.Date <= end && l.EndDate.Date >= start)
                .ToListAsync();

            var policies = await _context.LeavePolicies.ToDictionaryAsync(p => p.LeaveType);
            var holidays = await GetHolidaySetAsync(start.Year, end.Year);

            var byType = new Dictionary<LeaveType, decimal>();

            foreach (var leave in leaves)
            {
                if (!policies.TryGetValue(leave.LeaveType, out var policy))
                    continue;

                decimal days;
                if (leave.IsHalfDay)
                {
                    days = leave.StartDate.Date >= start && leave.StartDate.Date <= end ? 0.5m : 0m;
                }
                else
                {
                    var overlapStart = leave.StartDate.Date > start ? leave.StartDate.Date : start;
                    var overlapEnd = leave.EndDate.Date < end ? leave.EndDate.Date : end;
                    days = overlapStart > overlapEnd
                        ? 0
                        : LeaveDayCalculator.GetCountableDays(
                            overlapStart, overlapEnd, policy.ExcludeWeekends, policy.ExcludeHolidays, holidays).Count;
                }

                if (days <= 0)
                    continue;

                byType[leave.LeaveType] = byType.GetValueOrDefault(leave.LeaveType) + days;
            }

            var summary = new PayrollLeaveSummaryDto
            {
                EmployeeId = employeeId,
                PeriodStart = start,
                PeriodEnd = end
            };

            foreach (var (type, days) in byType)
            {
                var policy = policies[type];
                summary.ByType.Add(new PayrollLeaveTypeSummaryDto
                {
                    LeaveType = type,
                    LeaveTypeName = policy.Name,
                    IsPaid = policy.IsPaid,
                    Days = days
                });

                if (policy.IsPaid)
                    summary.TotalPaidLeaveDays += days;
                else
                    summary.TotalUnpaidLeaveDays += days;
            }

            return summary;
        }

        private async Task<HashSet<DateTime>> GetHolidaySetAsync(int startYear, int endYear)
        {
            var holidays = await _context.Holidays.ToListAsync();
            var set = new HashSet<DateTime>();

            for (var year = startYear; year <= endYear; year++)
            {
                foreach (var h in holidays)
                    set.Add(h.IsRecurringYearly ? new DateTime(year, h.Date.Month, h.Date.Day) : h.Date.Date);
            }

            return set;
        }
    }
}
