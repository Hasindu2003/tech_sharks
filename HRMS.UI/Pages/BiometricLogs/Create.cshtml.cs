using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Attendance;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRMS.UI.Pages.BiometricLogs
{
    [Authorize(Roles = "HR Manager")]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAttendanceService _attendanceService;
        private readonly ILogger<CreateModel> _logger;

        public List<Domain.Entities.Core.Employee> Employees { get; set; } = new();
        
        public CreateModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAttendanceService attendanceService,
            ILogger<CreateModel> logger)
        {
            _context = context;
            _userManager = userManager;
            _attendanceService = attendanceService;
            _logger = logger;
        }
        
        [BindProperty]
        public BiometricLog BiometricLog { get; set; } = new();

        [BindProperty]
        public IFormFile? CsvFile { get; set; }
        
        public async Task<IActionResult> OnGetAsync()
        {
            BiometricLog.LogDateTime = DateTime.Now;
            await LoadEmployeesAsync();
            return Page();
        }
        
        public async Task<IActionResult> OnPostAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Name == currentUser.Branch);
            int scopedBranchId = branch?.Id ?? -1;

            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Id == BiometricLog.EmployeeId);
            if (emp == null || emp.BranchId != scopedBranchId)
            {
                ModelState.AddModelError("BiometricLog.EmployeeId", "Employee not found or does not belong to your branch.");
            }

            if (!ModelState.IsValid)
            {
                await LoadEmployeesAsync();
                return Page();
            }

            try
            {
                var log = new BiometricLog
                {
                    EmployeeId = BiometricLog.EmployeeId,
                    LogDateTime = BiometricLog.LogDateTime,
                    DeviceId = BiometricLog.DeviceId,
                    LogType = BiometricLog.LogType ?? "checkIn"
                };
                await _attendanceService.ProcessAttendanceAsync(log);
                _logger.LogInformation("Biometric log created successfully for EmployeeId: {EmployeeId} in Branch: {BranchId}", log.EmployeeId, scopedBranchId);
                
                return RedirectToPage("/Attendance/Index");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error creating biometric log");
                ModelState.AddModelError(string.Empty, e.Message);
                await LoadEmployeesAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostImportCsvAsync()
        {
            if (CsvFile == null || CsvFile.Length == 0)
            {
                ModelState.AddModelError("CsvFile", "Please upload a valid CSV file.");
                await LoadEmployeesAsync();
                return Page();
            }

            var extension = System.IO.Path.GetExtension(CsvFile.FileName);
            if (extension.ToLower() != ".csv")
            {
                ModelState.AddModelError("CsvFile", "Only CSV files are allowed.");
                await LoadEmployeesAsync();
                return Page();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Name == currentUser.Branch);
            int scopedBranchId = branch?.Id ?? -1;

            int imported = 0;
            int skipped = 0;

            try
            {
                using var stream = CsvFile.OpenReadStream();
                using var reader = new System.IO.StreamReader(stream);
                
                var headerLine = await reader.ReadLineAsync();
                if (headerLine == null)
                {
                    ModelState.AddModelError("CsvFile", "The uploaded file is empty.");
                    await LoadEmployeesAsync();
                    return Page();
                }

                var headers = headerLine.Split(',').Select(h => h.Trim()).ToList();
                int idxUserId = headers.IndexOf("UserID");
                int idxVerifyTime = headers.IndexOf("VerifyTime");
                int idxVerifyState = headers.IndexOf("VerifyState");
                int idxDeviceId = headers.IndexOf("DeviceID");

                if (idxUserId == -1 || idxVerifyTime == -1 || idxDeviceId == -1)
                {
                    ModelState.AddModelError("CsvFile", "CSV headers must include UserID, VerifyTime, and DeviceID.");
                    await LoadEmployeesAsync();
                    return Page();
                }

                var branchEmployeeIds = await _context.Employees
                    .Where(e => e.BranchId == scopedBranchId && e.Status != "Draft" && e.NIC != "DUTY-ACC")
                    .Select(e => e.Id)
                    .ToListAsync();

                var branchEmployeeIdsSet = new HashSet<int>(branchEmployeeIds);

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split(',');
                    int maxIdx = Math.Max(idxUserId, Math.Max(idxVerifyTime, idxDeviceId));
                    if (idxVerifyState != -1)
                    {
                        maxIdx = Math.Max(maxIdx, idxVerifyState);
                    }

                    if (parts.Length <= maxIdx)
                    {
                        skipped++;
                        continue;
                    }

                    if (!int.TryParse(parts[idxUserId].Trim(), out int empId))
                    {
                        skipped++;
                        continue;
                    }

                    if (!branchEmployeeIdsSet.Contains(empId))
                    {
                        skipped++;
                        continue;
                    }

                    if (!DateTime.TryParse(parts[idxVerifyTime].Trim(), out DateTime logTime))
                    {
                        skipped++;
                        continue;
                    }

                    var devId = parts[idxDeviceId].Trim();
                    string logType = "checkIn";
                    if (idxVerifyState != -1)
                    {
                        var stateStr = parts[idxVerifyState].Trim();
                        if (stateStr == "1") logType = "checkOut";
                    }

                    try
                    {
                        var log = new BiometricLog
                        {
                            EmployeeId = empId,
                            LogDateTime = logTime,
                            DeviceId = devId,
                            LogType = logType
                        };
                        
                        await _attendanceService.ProcessAttendanceAsync(log);
                        imported++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed processing row for Employee {EmployeeId} at {Time}", empId, logTime);
                        skipped++;
                    }
                }

                TempData["SuccessMessage"] = $"Import completed successfully! {imported} log(s) imported, {skipped} log(s) skipped.";
                return RedirectToPage("/Attendance/Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing biometric CSV file");
                ModelState.AddModelError("CsvFile", $"Failed to parse file: {ex.Message}");
                await LoadEmployeesAsync();
                return Page();
            }
        }

        private async Task LoadEmployeesAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return;

            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Name == currentUser.Branch);
            int scopedBranchId = branch?.Id ?? -1;

            Employees = await _context.Employees
                .Where(e => e.BranchId == scopedBranchId && e.Status != "Draft" && e.NIC != "DUTY-ACC")
                .OrderBy(e => e.FullName)
                .ToListAsync();
        }
    }
}
