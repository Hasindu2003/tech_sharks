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
        public List<Domain.Entities.Core.Branch> Branches { get; set; } = new();
        public bool IsBranchManager { get; set; }
        public string? FixedBranchName { get; set; }
        public int? FixedBranchId { get; set; }
        
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
            ModelState.Remove("BiometricLog.Employee");
            ModelState.Remove("CsvFile");

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var (dutyEmployeeIds, dutyIdentifiers) = await GetDutyAccountExclusionsAsync();

            int scopedBranchId = -1;
            if (!string.IsNullOrWhiteSpace(currentUser.Branch))
            {
                var branchName = currentUser.Branch.Trim();
                var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Name.ToLower() == branchName.ToLower());
                scopedBranchId = branch?.Id ?? -1;
            }

            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Id == BiometricLog.EmployeeId);
            if (emp == null || emp.Status == "Draft" || emp.Status == "Terminated" || emp.Status == "Resigned"
                || emp.NIC.StartsWith("DUTY", StringComparison.OrdinalIgnoreCase) || emp.NIC == "DUTY-ACC"
                || (!string.IsNullOrEmpty(emp.EPFNumber) && emp.EPFNumber.StartsWith("DUTY", StringComparison.OrdinalIgnoreCase))
                || dutyEmployeeIds.Contains(emp.Id)
                || (!string.IsNullOrEmpty(emp.Email) && dutyIdentifiers.Contains(emp.Email.Trim()))
                || (!string.IsNullOrEmpty(emp.EPFNumber) && dutyIdentifiers.Contains(emp.EPFNumber.Trim())))
            {
                ModelState.AddModelError("BiometricLog.EmployeeId", "Selected employee was not found, is inactive, or is a duty account.");
            }
            else
            {
                // Validate permissions based on role
                if (User.IsInRole("HR Manager") || User.IsInRole("HR Officer"))
                {
                    // Access to all active branch employees
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

                    if (!assignedBranchIds.Contains(emp.BranchId))
                    {
                        ModelState.AddModelError("BiometricLog.EmployeeId", "Employee does not belong to any of your assigned branches.");
                    }
                }
                else
                {
                    // Branch Manager
                    if (scopedBranchId > 0 && emp.BranchId != scopedBranchId)
                    {
                        ModelState.AddModelError("BiometricLog.EmployeeId", "Employee does not belong to your branch.");
                    }
                }
            }

            if (BiometricLog.LogDateTime == default)
            {
                ModelState.AddModelError("BiometricLog.LogDateTime", "Please specify a valid scan timestamp.");
            }
            else if (BiometricLog.LogDateTime > SriLankaTime.Now.AddMinutes(1))
            {
                ModelState.AddModelError("BiometricLog.LogDateTime", "Cannot record attendance log for a future date and time that has not arrived yet.");
            }

            if (string.IsNullOrWhiteSpace(BiometricLog.DeviceId))
            {
                BiometricLog.DeviceId = "MANUAL-01";
                ModelState.Remove("BiometricLog.DeviceId");
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
                    DeviceId = BiometricLog.DeviceId.Trim(),
                    LogType = string.IsNullOrWhiteSpace(BiometricLog.LogType) ? null : BiometricLog.LogType.Trim()
                };
                await _attendanceService.ProcessAttendanceAsync(log);
                _logger.LogInformation("Manual biometric log created successfully for EmployeeId: {EmployeeId} ({Name}) at {LogDateTime}", 
                    log.EmployeeId, emp?.FullName, log.LogDateTime);
                
                var empDisplayName = !string.IsNullOrWhiteSpace(emp?.NameWithInitials) ? emp.NameWithInitials : emp?.FullName;
                TempData["SuccessMessage"] = $"Attendance log for {empDisplayName} ({emp?.EPFNumber}) recorded successfully.";
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

                var (dutyEmployeeIds, dutyIdentifiers) = await GetDutyAccountExclusionsAsync();

                List<int> validEmployeeIds;
                var baseQuery = _context.Employees
                    .Where(e => e.Status != "Draft" 
                             && e.Status != "Terminated" 
                             && e.Status != "Resigned"
                             && !e.NIC.StartsWith("DUTY") 
                             && e.NIC != "DUTY-ACC"
                             && !e.EPFNumber.StartsWith("DUTY"));

                if (User.IsInRole("HR Manager") || User.IsInRole("HR Officer"))
                {
                    // HR Manager / HR Officer: corporate-wide non-duty employees
                    var emps = await baseQuery.ToListAsync();
                    validEmployeeIds = emps
                        .Where(e => !dutyEmployeeIds.Contains(e.Id)
                                 && (string.IsNullOrEmpty(e.Email) || !dutyIdentifiers.Contains(e.Email.Trim()))
                                 && (string.IsNullOrEmpty(e.EPFNumber) || !dutyIdentifiers.Contains(e.EPFNumber.Trim())))
                        .Select(e => e.Id)
                        .ToList();
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

                    var emps = await baseQuery
                        .Where(e => assignedBranchIds.Contains(e.BranchId))
                        .ToListAsync();

                    validEmployeeIds = emps
                        .Where(e => !dutyEmployeeIds.Contains(e.Id)
                                 && (string.IsNullOrEmpty(e.Email) || !dutyIdentifiers.Contains(e.Email.Trim()))
                                 && (string.IsNullOrEmpty(e.EPFNumber) || !dutyIdentifiers.Contains(e.EPFNumber.Trim())))
                        .Select(e => e.Id)
                        .ToList();
                }
                else
                {
                    // Branch Manager: strictly employees of their own branch only
                    var emps = await baseQuery
                        .Where(e => e.BranchId == scopedBranchId)
                        .ToListAsync();

                    validEmployeeIds = emps
                        .Where(e => !dutyEmployeeIds.Contains(e.Id)
                                 && (string.IsNullOrEmpty(e.Email) || !dutyIdentifiers.Contains(e.Email.Trim()))
                                 && (string.IsNullOrEmpty(e.EPFNumber) || !dutyIdentifiers.Contains(e.EPFNumber.Trim())))
                        .Select(e => e.Id)
                        .ToList();
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

                        var timeStr = parts[idxVerifyTime].Trim();
                        if (!DateTime.TryParse(timeStr, out logTime))
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
                            var vState = parts[idxVerifyState].Trim().ToLower();
                            if (vState == "0" || vState == "checkin" || vState == "check-in" || vState == "in")
                                logType = "checkIn";
                            else if (vState == "1" || vState == "checkout" || vState == "check-out" || vState == "out")
                                logType = "checkOut";
                        }
                    }

                    if (!branchEmployeeIdsSet.Contains(empId))
                    {
                        skipped++;
                        continue;
                    }

                    if (logTime > SriLankaTime.Now.AddMinutes(1))
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
                        _logger.LogWarning(ex, "Failed to import punch log for Employee {EmpId} at {Time}", empId, logTime);
                        skipped++;
                    }
                }

                _logger.LogInformation("Import biometric punches completed. Imported: {Imported}, Skipped: {Skipped}", imported, skipped);

                if (imported > 0)
                {
                    TempData["SuccessMessage"] = $"Successfully imported {imported} punch log(s). {skipped} row(s) skipped (unmatched or non-branch employees).";
                    return RedirectToPage("/Attendance/Index");
                }
                else
                {
                    ModelState.AddModelError("CsvFile", $"No valid punch records were imported ({skipped} rows skipped). Ensure employee IDs in the file correspond to employees in your branch.");
                    await LoadEmployeesAsync();
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing uploaded file");
                ModelState.AddModelError("CsvFile", "An unexpected error occurred while parsing the uploaded file: " + ex.Message);
                await LoadEmployeesAsync();
                return Page();
            }
        }

        private static bool TryParseBiometricDateTime(string dateStr, string timeStr, out DateTime dt)
        {
            dt = default;
            var combined = $"{dateStr} {timeStr}".Trim();
            string[] formats = {
                "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd H:mm:ss",
                "yyyy/MM/dd HH:mm:ss", "yyyy/MM/dd H:mm:ss",
                "dd-MM-yyyy HH:mm:ss", "dd-MM-yyyy H:mm:ss",
                "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy H:mm:ss",
                "MM/dd/yyyy HH:mm:ss", "MM/dd/yyyy H:mm:ss",
                "yyyy-MM-dd hh:mm:ss tt", "yyyy/MM/dd hh:mm:ss tt",
                "dd-MM-yyyy hh:mm:ss tt", "dd/MM/yyyy hh:mm:ss tt",
                "MM/dd/yyyy hh:mm:ss tt",
                "yyyy-MM-dd HH:mm", "yyyy/MM/dd HH:mm",
                "dd-MM-yyyy HH:mm", "dd/MM/yyyy HH:mm",
                "MM/dd/yyyy HH:mm",
                "yyyy-MM-dd", "dd/MM/yyyy"
            };

            if (DateTime.TryParseExact(combined, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
            {
                return true;
            }

            return DateTime.TryParse(combined, out dt);
        }

        private static List<List<string>> ReadXlsxRows(System.IO.Stream stream)
        {
            var rows = new List<List<string>>();

            using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read))
            {
                // 1. Read shared strings
                var sharedStrings = new List<string>();
                var sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
                if (sharedStringsEntry != null)
                {
                    using var sStream = sharedStringsEntry.Open();
                    var xdoc = System.Xml.Linq.XDocument.Load(sStream);
                    var ns = xdoc.Root?.GetDefaultNamespace() ?? System.Xml.Linq.XNamespace.None;
                    foreach (var si in xdoc.Descendants(ns + "si"))
                    {
                        var tElem = si.Element(ns + "t");
                        if (tElem != null)
                        {
                            sharedStrings.Add(tElem.Value);
                        }
                        else
                        {
                            sharedStrings.Add(string.Concat(si.Descendants(ns + "t").Select(t => t.Value)));
                        }
                    }
                }

                // 2. Read first worksheet
                var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
                if (sheetEntry == null) return rows;

                using var wStream = sheetEntry.Open();
                var sheetDoc = System.Xml.Linq.XDocument.Load(wStream);
                var wns = sheetDoc.Root?.GetDefaultNamespace() ?? System.Xml.Linq.XNamespace.None;
                var sheetData = sheetDoc.Descendants(wns + "sheetData").FirstOrDefault();
                if (sheetData == null) return rows;

                foreach (var row in sheetData.Elements(wns + "row"))
                {
                    var rowValues = new List<string>();
                    int currentCol = 0;

                    foreach (var c in row.Elements(wns + "c"))
                    {
                        var cellRef = (string?)c.Attribute("r") ?? "";
                        int colIndex = GetColumnIndex(cellRef);
                        while (currentCol < colIndex)
                        {
                            rowValues.Add("");
                            currentCol++;
                        }

                        var tAttr = (string?)c.Attribute("t");
                        var vElem = c.Element(wns + "v");
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
                            var isElem = c.Element(wns + "is");
                            if (isElem != null)
                            {
                                cellValue = string.Concat(isElem.Descendants(wns + "t").Select(t => t.Value));
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

        private async Task<(HashSet<int> DutyEmployeeIds, HashSet<string> DutyIdentifiers)> GetDutyAccountExclusionsAsync()
        {
            var dutyEmployeeIds = new HashSet<int>();
            var dutyIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var dutyRoles = new[] { "Admin", "HR Manager", "HR Officer", "Branch Manager", "Area Manager", "Department Head", "Welfare Manager" };
            foreach (var role in dutyRoles)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                foreach (var u in usersInRole)
                {
                    if (u.EmployeeId.HasValue && u.EmployeeId.Value > 0)
                        dutyEmployeeIds.Add(u.EmployeeId.Value);

                    if (!string.IsNullOrWhiteSpace(u.Email))
                        dutyIdentifiers.Add(u.Email.Trim());

                    if (!string.IsNullOrWhiteSpace(u.UserName))
                        dutyIdentifiers.Add(u.UserName.Trim());

                    if (!string.IsNullOrWhiteSpace(u.EpfNumber))
                        dutyIdentifiers.Add(u.EpfNumber.Trim());
                }
            }

            return (dutyEmployeeIds, dutyIdentifiers);
        }

        private async Task LoadEmployeesAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return;

            var (dutyEmployeeIds, dutyIdentifiers) = await GetDutyAccountExclusionsAsync();

            int scopedBranchId = -1;
            if (!string.IsNullOrWhiteSpace(currentUser.Branch))
            {
                var branchName = currentUser.Branch.Trim();
                var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Name.ToLower() == branchName.ToLower());
                scopedBranchId = branch?.Id ?? -1;
            }

            IsBranchManager = User.IsInRole("Branch Manager") || User.IsInRole("Department Head");
            if (IsBranchManager && scopedBranchId > 0)
            {
                var br = await _context.Branches.FindAsync(scopedBranchId);
                FixedBranchName = br?.Name ?? currentUser.Branch;
                FixedBranchId = scopedBranchId;
                Branches = br != null ? new List<Domain.Entities.Core.Branch> { br } : new();
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

                Branches = await _context.Branches
                    .Where(b => assignedBranchIds.Contains(b.Id))
                    .OrderBy(b => b.Name)
                    .ToListAsync();
            }
            else
            {
                // HR Manager & HR Officer
                Branches = await _context.Branches
                    .OrderBy(b => b.Name)
                    .ToListAsync();
            }

            var query = _context.Employees
                .Include(e => e.Branch)
                .Where(e => e.Status != "Draft" 
                         && e.Status != "Terminated" 
                         && e.Status != "Resigned"
                         && !e.NIC.StartsWith("DUTY")
                         && e.NIC != "DUTY-ACC"
                         && !e.EPFNumber.StartsWith("DUTY"));

            if (IsBranchManager && scopedBranchId > 0)
            {
                query = query.Where(e => e.BranchId == scopedBranchId);
            }
            else if (User.IsInRole("Area Manager"))
            {
                var allowedBranchIds = Branches.Select(b => b.Id).ToList();
                query = query.Where(e => allowedBranchIds.Contains(e.BranchId));
            }

            var dbEmployees = await query.OrderBy(e => e.FullName).ToListAsync();

            Employees = dbEmployees
                .Where(e => !dutyEmployeeIds.Contains(e.Id)
                         && (string.IsNullOrEmpty(e.Email) || !dutyIdentifiers.Contains(e.Email.Trim()))
                         && (string.IsNullOrEmpty(e.EPFNumber) || !dutyIdentifiers.Contains(e.EPFNumber.Trim()))
                         && !e.FullName.Contains("Duty Account", StringComparison.OrdinalIgnoreCase)
                         && !e.FullName.Equals("Welfare Manager", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}
