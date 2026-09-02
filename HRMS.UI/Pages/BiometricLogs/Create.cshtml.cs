using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Attendance;
using HRMS.Domain.Common;
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
    [Authorize(Roles = "Branch Manager,HR Manager,HR Officer,Area Manager")]
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
            BiometricLog.LogDateTime = SriLankaTime.Now;
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

            var logDate = BiometricLog.LogDateTime.Date;
            var existingAtt = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == BiometricLog.EmployeeId && a.Date == logDate);

            if (existingAtt != null && existingAtt.TimeIn.HasValue && existingAtt.TimeOut.HasValue)
            {
                ModelState.AddModelError(string.Empty, $"Full attendance (Check-In: {existingAtt.TimeIn:hh:mm tt}, Check-Out: {existingAtt.TimeOut:hh:mm tt}) is already recorded for this employee on {logDate:yyyy-MM-dd}. Additional records cannot be added.");
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
                ModelState.AddModelError("CsvFile", "Please upload a valid Excel (.xlsx) or CSV (.csv) file.");
                await LoadEmployeesAsync();
                return Page();
            }

            var extension = System.IO.Path.GetExtension(CsvFile.FileName).ToLower();
            if (extension != ".csv" && extension != ".xlsx")
            {
                ModelState.AddModelError("CsvFile", "Only Excel (.xlsx) and CSV (.csv) files are supported.");
                await LoadEmployeesAsync();
                return Page();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            int imported = 0;
            int skipped = 0;

            try
            {
                List<List<string>> rows = new();

                if (extension == ".xlsx")
                {
                    using var stream = CsvFile.OpenReadStream();
                    rows = ReadXlsxRows(stream);
                }
                else
                {
                    using var stream = CsvFile.OpenReadStream();
                    using var reader = new System.IO.StreamReader(stream);
                    while (!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            var cells = line.Split(',').Select(c => c.Trim().Trim('"')).ToList();
                            rows.Add(cells);
                        }
                    }
                }

                if (rows.Count < 2)
                {
                    ModelState.AddModelError("CsvFile", "The uploaded file does not contain enough data rows.");
                    await LoadEmployeesAsync();
                    return Page();
                }

                var headers = rows[0].Select(h => h.Trim()).ToList();

                // Format 1: Machine export (First Name, Last Name, ID, Department, Date, Time, Device Serial No., Punch State)
                int idxId = headers.FindIndex(h => h.Equals("ID", StringComparison.OrdinalIgnoreCase));
                int idxDate = headers.FindIndex(h => h.Equals("Date", StringComparison.OrdinalIgnoreCase));
                int idxTime = headers.FindIndex(h => h.Equals("Time", StringComparison.OrdinalIgnoreCase));
                int idxDeviceSerial = headers.FindIndex(h => h.Equals("Device Serial No.", StringComparison.OrdinalIgnoreCase) || h.Equals("Device Serial No", StringComparison.OrdinalIgnoreCase) || h.Equals("DeviceSerialNo", StringComparison.OrdinalIgnoreCase));
                int idxDeviceName = headers.FindIndex(h => h.Equals("Device Name", StringComparison.OrdinalIgnoreCase) || h.Equals("DeviceName", StringComparison.OrdinalIgnoreCase));
                int idxPunchState = headers.FindIndex(h => h.Equals("Punch State", StringComparison.OrdinalIgnoreCase) || h.Equals("PunchState", StringComparison.OrdinalIgnoreCase));

                // Format 2: Legacy format (UserID, VerifyTime, VerifyState, DeviceID)
                int idxUserId = headers.FindIndex(h => h.Equals("UserID", StringComparison.OrdinalIgnoreCase) || h.Equals("EmployeeID", StringComparison.OrdinalIgnoreCase));
                int idxVerifyTime = headers.FindIndex(h => h.Equals("VerifyTime", StringComparison.OrdinalIgnoreCase) || h.Equals("LogTime", StringComparison.OrdinalIgnoreCase) || h.Equals("ScanTime", StringComparison.OrdinalIgnoreCase));
                int idxVerifyState = headers.FindIndex(h => h.Equals("VerifyState", StringComparison.OrdinalIgnoreCase) || h.Equals("PunchType", StringComparison.OrdinalIgnoreCase));
                int idxDeviceId = headers.FindIndex(h => h.Equals("DeviceID", StringComparison.OrdinalIgnoreCase) || h.Equals("Device_ID", StringComparison.OrdinalIgnoreCase));

                bool isMachineFormat = idxId != -1 && idxDate != -1 && idxTime != -1;
                bool isLegacyFormat = idxUserId != -1 && idxVerifyTime != -1;

                if (!isMachineFormat && !isLegacyFormat)
                {
                    ModelState.AddModelError("CsvFile", "Unrecognized header format. File must contain either [ID, Date, Time] or [UserID, VerifyTime] columns.");
                    await LoadEmployeesAsync();
                    return Page();
                }

                int scopedBranchId = -1;
                if (!string.IsNullOrWhiteSpace(currentUser.Branch))
                {
                    var branchName = currentUser.Branch.Trim();
                    var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Name.ToLower() == branchName.ToLower());
                    scopedBranchId = branch?.Id ?? -1;
                }

                List<int> validEmployeeIds;
                if (User.IsInRole("HR Manager") || User.IsInRole("HR Officer"))
                {
                    // HR Manager / HR Officer: corporate-wide
                    validEmployeeIds = await _context.Employees
                        .Where(e => e.Status != "Draft" && e.NIC != "DUTY-ACC")
                        .Select(e => e.Id)
                        .ToListAsync();
                }
                else if (User.IsInRole("Area Manager"))
                {
                    // Area Manager: assigned regional branches
                    var managedStr = currentUser.ManagedBranches ?? "";
                    var assignedBranchIds = managedStr
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                        .Where(id => id > 0)
                        .ToList();

                    if (!assignedBranchIds.Any() && scopedBranchId > 0) assignedBranchIds.Add(scopedBranchId);

                    validEmployeeIds = await _context.Employees
                        .Where(e => assignedBranchIds.Contains(e.BranchId) && e.Status != "Draft" && e.NIC != "DUTY-ACC")
                        .Select(e => e.Id)
                        .ToListAsync();
                }
                else
                {
                    // Branch Manager: strictly employees of their own branch only
                    validEmployeeIds = await _context.Employees
                        .Where(e => e.BranchId == scopedBranchId && e.Status != "Draft" && e.NIC != "DUTY-ACC")
                        .Select(e => e.Id)
                        .ToListAsync();
                }

                var branchEmployeeIdsSet = new HashSet<int>(validEmployeeIds);

                for (int r = 1; r < rows.Count; r++)
                {
                    var parts = rows[r];
                    if (parts.All(string.IsNullOrWhiteSpace)) continue;

                    int empId = 0;
                    DateTime logTime = default;
                    string devId = "BIO-DEVICE";
                    string? logType = null;

                    if (isMachineFormat)
                    {
                        if (parts.Count <= Math.Max(idxId, Math.Max(idxDate, idxTime)))
                        {
                            skipped++;
                            continue;
                        }

                        var rawIdStr = parts[idxId].Trim();
                        // Support numeric ("0032", "32") and alphanumeric prefix ("E0032", "EMP0032")
                        var cleanIdStr = System.Text.RegularExpressions.Regex.Replace(rawIdStr, @"^[^\d]+", "");
                        if (!int.TryParse(cleanIdStr, out empId) || empId <= 0)
                        {
                            skipped++;
                            continue;
                        }

                        var dateStr = parts[idxDate].Trim();
                        var timeStr = parts[idxTime].Trim();
                        if (!TryParseBiometricDateTime(dateStr, timeStr, out logTime))
                        {
                            skipped++;
                            continue;
                        }

                        if (idxDeviceSerial != -1 && idxDeviceSerial < parts.Count && !string.IsNullOrWhiteSpace(parts[idxDeviceSerial]))
                        {
                            devId = parts[idxDeviceSerial].Trim();
                        }
                        else if (idxDeviceName != -1 && idxDeviceName < parts.Count && !string.IsNullOrWhiteSpace(parts[idxDeviceName]))
                        {
                            devId = parts[idxDeviceName].Trim();
                        }

                        if (idxPunchState != -1 && idxPunchState < parts.Count)
                        {
                            var pState = parts[idxPunchState].Trim().ToLower();
                            if (pState == "0" || pState == "checkin" || pState == "check-in" || pState == "in")
                                logType = "checkIn";
                            else if (pState == "1" || pState == "checkout" || pState == "check-out" || pState == "out")
                                logType = "checkOut";
                        }
                    }
                    else // Legacy format
                    {
                        if (parts.Count <= Math.Max(idxUserId, idxVerifyTime))
                        {
                            skipped++;
                            continue;
                        }

                        var rawLegacyId = parts[idxUserId].Trim();
                        var cleanLegacyId = System.Text.RegularExpressions.Regex.Replace(rawLegacyId, @"^[^\d]+", "");
                        if (!int.TryParse(cleanLegacyId, out empId) || empId <= 0)
                        {
                            skipped++;
                            continue;
                        }

                        var verifyTimeStr = parts[idxVerifyTime].Trim();
                        if (!TryParseBiometricDateTime(verifyTimeStr, "", out logTime))
                        {
                            skipped++;
                            continue;
                        }

                        if (idxDeviceId != -1 && idxDeviceId < parts.Count && !string.IsNullOrWhiteSpace(parts[idxDeviceId]))
                        {
                            devId = parts[idxDeviceId].Trim();
                        }

                        if (idxVerifyState != -1 && idxVerifyState < parts.Count)
                        {
                            var stateStr = parts[idxVerifyState].Trim().ToLower();
                            if (stateStr == "1" || stateStr == "checkout" || stateStr == "out")
                                logType = "checkOut";
                            else if (stateStr == "0" || stateStr == "checkin" || stateStr == "in")
                                logType = "checkIn";
                        }
                    }

                    // Check if employee belongs to this branch
                    if (!branchEmployeeIdsSet.Contains(empId))
                    {
                        skipped++;
                        continue;
                    }

                    try
                    {
                        var log = new BiometricLog
                        {
                            EmployeeId = empId,
                            LogDateTime = logTime,
                            DeviceId = devId,
                            LogType = logType ?? "checkIn"
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

                if (imported > 0)
                {
                    TempData["SuccessMessage"] = $"Import completed successfully! {imported} punch log(s) imported for your branch." + (skipped > 0 ? $" ({skipped} record(s) were skipped due to branch filter or duplicates)" : "");
                }
                else
                {
                    TempData["ErrorMessage"] = $"No logs were imported. {skipped} record(s) in the file were skipped (verify that employee IDs belong to your branch: {currentUser.Branch ?? "N/A"}).";
                }
                return RedirectToPage("/Attendance/Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing biometric file");
                ModelState.AddModelError("CsvFile", $"Failed to parse file: {ex.Message}");
                await LoadEmployeesAsync();
                return Page();
            }
        }

        private static bool TryParseBiometricDateTime(string dateStr, string timeStr, out DateTime result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(dateStr)) return false;

            // Single combined string scenario
            if (string.IsNullOrWhiteSpace(timeStr))
            {
                if (double.TryParse(dateStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double oaFull) && oaFull > 30000)
                {
                    try { result = DateTime.FromOADate(oaFull); return true; } catch { }
                }
                return DateTime.TryParse(dateStr, out result);
            }

            // Parse Date component
            DateTime baseDate;
            if (double.TryParse(dateStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double oaDate) && oaDate > 30000)
            {
                try { baseDate = DateTime.FromOADate(oaDate).Date; }
                catch { return false; }
            }
            else if (!DateTime.TryParse(dateStr, out baseDate))
            {
                return false;
            }

            // Parse Time component
            // Excel time serial (e.g. 0.3298611111111111 -> 07:55:00)
            if (double.TryParse(timeStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double timeFraction) && timeFraction >= 0 && timeFraction <= 1.0)
            {
                var timeSpan = TimeSpan.FromDays(timeFraction);
                result = baseDate.Date + timeSpan;
                return true;
            }

            // Standard TimeSpan or Time string (e.g. "07:55", "07:55:00", "7:55 AM")
            if (TimeSpan.TryParse(timeStr, out var parsedSpan))
            {
                result = baseDate.Date + parsedSpan;
                return true;
            }

            if (DateTime.TryParse($"{dateStr} {timeStr}", out result))
            {
                return true;
            }

            if (DateTime.TryParse(timeStr, out var timeObj))
            {
                result = baseDate.Date + timeObj.TimeOfDay;
                return true;
            }

            return false;
        }

        private static List<List<string>> ReadXlsxRows(System.IO.Stream stream)
        {
            var rows = new List<List<string>>();
            using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read, true);

            var sharedStrings = new List<string>();
            var sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
            if (sharedStringsEntry != null)
            {
                using var sStream = sharedStringsEntry.Open();
                var sDoc = System.Xml.Linq.XDocument.Load(sStream);
                System.Xml.Linq.XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                foreach (var si in sDoc.Descendants(ns + "si"))
                {
                    var text = string.Concat(si.Descendants(ns + "t").Select(t => t.Value));
                    sharedStrings.Add(text);
                }
            }

            var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml") ?? archive.Entries.FirstOrDefault(e => e.FullName.StartsWith("xl/worksheets/sheet") && e.FullName.EndsWith(".xml"));
            if (sheetEntry != null)
            {
                using var sheetStream = sheetEntry.Open();
                var sheetDoc = System.Xml.Linq.XDocument.Load(sheetStream);
                System.Xml.Linq.XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                foreach (var rowElem in sheetDoc.Descendants(ns + "row"))
                {
                    var rowValues = new List<string>();
                    int currentCol = 0;
                    foreach (var c in rowElem.Elements(ns + "c"))
                    {
                        var rAttr = (string?)c.Attribute("r");
                        if (!string.IsNullOrEmpty(rAttr))
                        {
                            int colIndex = GetColumnIndex(rAttr);
                            while (currentCol < colIndex)
                            {
                                rowValues.Add("");
                                currentCol++;
                            }
                        }

                        var tAttr = (string?)c.Attribute("t");
                        var vElem = c.Element(ns + "v");
                        string cellValue = "";
                        if (vElem != null)
                        {
                            if (tAttr == "s" && int.TryParse(vElem.Value, out int sIndex) && sIndex >= 0 && sIndex < sharedStrings.Count)
                            {
                                cellValue = sharedStrings[sIndex];
                            }
                            else
                            {
                                cellValue = vElem.Value;
                            }
                        }
                        else
                        {
                            var isElem = c.Element(ns + "is");
                            if (isElem != null)
                            {
                                cellValue = string.Concat(isElem.Descendants(ns + "t").Select(t => t.Value));
                            }
                        }

                        rowValues.Add(cellValue);
                        currentCol++;
                    }
                    if (rowValues.Any(v => !string.IsNullOrWhiteSpace(v)))
                    {
                        rows.Add(rowValues);
                    }
                }
            }

            return rows;
        }

        private static int GetColumnIndex(string cellReference)
        {
            int index = 0;
            foreach (char ch in cellReference)
            {
                if (char.IsLetter(ch))
                {
                    index = index * 26 + (char.ToUpper(ch) - 'A' + 1);
                }
                else break;
            }
            return index - 1;
        }

        private async Task LoadEmployeesAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return;

            int scopedBranchId = -1;
            if (!string.IsNullOrWhiteSpace(currentUser.Branch))
            {
                var branchName = currentUser.Branch.Trim();
                var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Name.ToLower() == branchName.ToLower());
                scopedBranchId = branch?.Id ?? -1;
            }

            if (User.IsInRole("HR Manager") || User.IsInRole("HR Officer"))
            {
                Employees = await _context.Employees
                    .Where(e => e.Status != "Draft" && e.NIC != "DUTY-ACC")
                    .OrderBy(e => e.FullName)
                    .ToListAsync();
            }
            else if (User.IsInRole("Area Manager"))
            {
                var managedStr = currentUser.ManagedBranches ?? "";
                var assignedBranchIds = managedStr
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                    .Where(id => id > 0)
                    .ToList();

                if (!assignedBranchIds.Any() && scopedBranchId > 0) assignedBranchIds.Add(scopedBranchId);

                Employees = await _context.Employees
                    .Where(e => assignedBranchIds.Contains(e.BranchId) && e.Status != "Draft" && e.NIC != "DUTY-ACC")
                    .OrderBy(e => e.FullName)
                    .ToListAsync();
            }
            else
            {
                Employees = await _context.Employees
                    .Where(e => e.BranchId == scopedBranchId && e.Status != "Draft" && e.NIC != "DUTY-ACC")
                    .OrderBy(e => e.FullName)
                    .ToListAsync();
            }
        }
    }
}
