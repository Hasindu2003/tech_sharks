using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.Settings.OvertimePolicy
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public IndexModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public PayrollPolicySetting GlobalPolicy { get; set; } = new();
        public List<BranchPolicyItem> BranchPolicies { get; set; } = new();
        public List<Branch> Branches { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostSaveGlobalPolicyAsync(
            int workingDays, 
            decimal dailyHours, 
            decimal otMultiplier, 
            decimal weekendMultiplier)
        {
            var global = await _db.PayrollPolicySettings
                .FirstOrDefaultAsync(p => p.BranchId == null);

            if (global == null)
            {
                global = new PayrollPolicySetting
                {
                    BranchId = null
                };
                _db.PayrollPolicySettings.Add(global);
            }

            global.StandardMonthlyWorkingDays = Math.Max(1, Math.Min(31, workingDays));
            global.StandardDailyWorkingHours = Math.Max(1.0m, Math.Min(24.0m, dailyHours));
            global.StandardOtMultiplier = Math.Max(1.0m, Math.Min(5.0m, otMultiplier));
            global.WeekendOtMultiplier = Math.Max(1.0m, Math.Min(5.0m, weekendMultiplier));
            global.AutoCalculateOtOnPayroll = true;
            global.LastModifiedDate = DateTime.Now;
            global.ModifiedBy = User.Identity?.Name ?? "Admin";

            await _db.SaveChangesAsync();

            TempData["Success"] = "Corporate Overtime & Working Hours Policy updated successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSaveBranchOverrideAsync(
            int branchId,
            bool hasOverride,
            int? workingDays,
            decimal? dailyHours,
            decimal? otMultiplier,
            decimal? weekendMultiplier)
        {
            var branchSetting = await _db.PayrollPolicySettings
                .FirstOrDefaultAsync(p => p.BranchId == branchId);

            if (!hasOverride)
            {
                if (branchSetting != null)
                {
                    _db.PayrollPolicySettings.Remove(branchSetting);
                    await _db.SaveChangesAsync();
                }
                TempData["Success"] = "Branch override removed. Branch now follows Corporate Global Policy.";
                return RedirectToPage();
            }

            if (branchSetting == null)
            {
                branchSetting = new PayrollPolicySetting
                {
                    BranchId = branchId
                };
                _db.PayrollPolicySettings.Add(branchSetting);
            }

            branchSetting.StandardMonthlyWorkingDays = Math.Max(1, Math.Min(31, workingDays ?? 21));
            branchSetting.StandardDailyWorkingHours = Math.Max(1.0m, Math.Min(24.0m, dailyHours ?? 8.0m));
            branchSetting.StandardOtMultiplier = Math.Max(1.0m, Math.Min(5.0m, otMultiplier ?? 1.5m));
            branchSetting.WeekendOtMultiplier = Math.Max(1.0m, Math.Min(5.0m, weekendMultiplier ?? 2.0m));
            branchSetting.AutoCalculateOtOnPayroll = true;
            branchSetting.LastModifiedDate = DateTime.Now;
            branchSetting.ModifiedBy = User.Identity?.Name ?? "Admin";

            await _db.SaveChangesAsync();

            TempData["Success"] = "Branch-specific OT policy updated successfully.";
            return RedirectToPage();
        }

        private async Task LoadDataAsync()
        {
            GlobalPolicy = await _db.PayrollPolicySettings
                .FirstOrDefaultAsync(p => p.BranchId == null)
                ?? new PayrollPolicySetting
                {
                    StandardMonthlyWorkingDays = 21,
                    StandardDailyWorkingHours = 8.0m,
                    StandardOtMultiplier = 1.5m,
                    WeekendOtMultiplier = 2.0m,
                    AutoCalculateOtOnPayroll = true
                };

            Branches = await _db.Branches.OrderBy(b => b.Name).ToListAsync();

            var overrides = await _db.PayrollPolicySettings
                .Where(p => p.BranchId != null)
                .ToListAsync();

            BranchPolicies = Branches.Select(b =>
            {
                var custom = overrides.FirstOrDefault(o => o.BranchId == b.Id);
                return new BranchPolicyItem
                {
                    BranchId = b.Id,
                    BranchName = b.Name,
                    HasCustomOverride = custom != null,
                    WorkingDays = custom?.StandardMonthlyWorkingDays ?? GlobalPolicy.StandardMonthlyWorkingDays,
                    DailyHours = custom?.StandardDailyWorkingHours ?? GlobalPolicy.StandardDailyWorkingHours,
                    OtMultiplier = custom?.StandardOtMultiplier ?? GlobalPolicy.StandardOtMultiplier,
                    WeekendMultiplier = custom?.WeekendOtMultiplier ?? GlobalPolicy.WeekendOtMultiplier,
                    AutoCalculate = custom?.AutoCalculateOtOnPayroll ?? GlobalPolicy.AutoCalculateOtOnPayroll,
                    LastModified = custom?.LastModifiedDate,
                    ModifiedBy = custom?.ModifiedBy
                };
            }).ToList();
        }
    }

    public class BranchPolicyItem
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public bool HasCustomOverride { get; set; }
        public int WorkingDays { get; set; }
        public decimal DailyHours { get; set; }
        public decimal OtMultiplier { get; set; }
        public decimal WeekendMultiplier { get; set; }
        public bool AutoCalculate { get; set; }
        public DateTime? LastModified { get; set; }
        public string? ModifiedBy { get; set; }
    }
}
