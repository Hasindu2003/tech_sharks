using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Persistence;
using HRMS.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRMS.UI.Services.Impl
{
    public class LeaveService : ILeaveService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LeaveService> _logger;
        private readonly INotificationService _notificationService;

        public LeaveService(ApplicationDbContext context, ILogger<LeaveService> logger, INotificationService notificationService)
        {
            _context = context;
            _logger = logger;
            _notificationService = notificationService;
        }

        public async Task<LeaveEntitlement> GetLeaveBalanceAsync(int employeeId, string leaveType, int year)
        {
            var entitlement = await _context.LeaveEntitlements
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.LeaveType == leaveType && e.Year == year);

            if (entitlement == null)
            {
                var emp = await _context.Employees.FindAsync(employeeId);
                var empType = NormalizeEmployeeType(emp?.EmployeeType);
                int defaultDays = await GetDefaultLeaveDaysAsync(leaveType, empType);
                entitlement = new LeaveEntitlement
                {
                    EmployeeId = employeeId,
                    LeaveType = leaveType,
                    Year = year,
                    TotalDays = defaultDays,
                    UsedDays = 0,
                    RemainingDays = defaultDays
                };
                _context.LeaveEntitlements.Add(entitlement);
                await _context.SaveChangesAsync();
            }

            return entitlement;
        }

        public async Task<List<LeaveEntitlement>> GetAllLeaveBalancesAsync(int employeeId, int year)
        {
            var leaveTypes = new[] { "Annual", "Casual", "Medical", "Maternity", "Overseas", "Exam", "Bereavement", "Other" };
            var balances = new List<LeaveEntitlement>();

            foreach (var type in leaveTypes)
            {
                var balance = await GetLeaveBalanceAsync(employeeId, type, year);
                balances.Add(balance);
            }

            return balances;
        }

        public async Task<string> GetApplicantWorkflowRoleAsync(Domain.Entities.Core.Employee applicant)
        {
            var designationTitle = applicant.Designation?.Title;
            if (string.IsNullOrEmpty(designationTitle) && applicant.DesignationId.HasValue)
            {
                var desig = await _context.Designations.FindAsync(applicant.DesignationId.Value);
                designationTitle = desig?.Title;
            }

            if (string.Equals(designationTitle, "Area Manager", StringComparison.OrdinalIgnoreCase))
                return "Area Manager";
            if (string.Equals(designationTitle, "Branch Manager", StringComparison.OrdinalIgnoreCase))
                return "Branch Manager";
            if (string.Equals(designationTitle, "Department Head", StringComparison.OrdinalIgnoreCase))
                return "Department Head";

            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmployeeId == applicant.Id || u.Email == applicant.Email);
            if (user != null)
            {
                var userRoles = await (from ur in _context.UserRoles
                                       join r in _context.Roles on ur.RoleId equals r.Id
                                       where ur.UserId == user.Id
                                       select r.Name).ToListAsync();

                if (userRoles.Contains("Area Manager")) return "Area Manager";
                if (userRoles.Contains("Branch Manager")) return "Branch Manager";
                if (userRoles.Contains("Department Head")) return "Department Head";
            }

            return "Employee";
        }

        public async Task<Leave> ApplyLeaveAsync(Leave leave)
        {
            var employee = await _context.Employees
                .Include(e => e.Branch)
                .Include(e => e.Department)
                .Include(e => e.Designation)
                .FirstOrDefaultAsync(e => e.Id == leave.EmployeeId);

            if (employee == null)
                throw new Exception("Employee not found");

            if (leave.StartDate.Date < DateTime.Today.AddDays(-2))
                throw new Exception("Leave start date cannot be more than 2 days in the past.");

            if (leave.EndDate.Date < leave.StartDate.Date)
                throw new Exception("End date cannot be earlier than start date.");

            leave.AppliedDate = HRMS.Domain.Common.SriLankaTime.Now;

            if (leave.IsHalfDay)
            {
                if (!leave.LeaveType.Equals("Casual", StringComparison.OrdinalIgnoreCase) &&
                    !leave.LeaveType.Equals("Annual", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("Half-day leave is only permitted for Casual Leave and Annual Leave.");
                }

                if (leave.StartDate.Date != leave.EndDate.Date)
                {
                    leave.EndDate = leave.StartDate.Date;
                }

                if (leave.StartDate.DayOfWeek == DayOfWeek.Saturday || leave.StartDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    throw new Exception("Half-day leave cannot be applied on weekends.");
                }

                leave.TotalDays = 0.5;
                if (string.IsNullOrWhiteSpace(leave.HalfDaySession))
                {
                    leave.HalfDaySession = "First Half (Morning)";
                }
            }
            else
            {
                leave.IsHalfDay = false;
                leave.HalfDaySession = null;
                leave.TotalDays = await CalculateLeaveDaysAsync(leave.StartDate, leave.EndDate);
            }
            
            if (leave.TotalDays <= 0)
                throw new Exception("The selected date range does not contain any working days (weekends are excluded).");

            if (leave.LeaveType == "Maternity" && employee.Sex != null && employee.Sex.Equals("Male", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Male employees are not eligible for Maternity Leave.");

            var overlappingLeaves = await _context.Leaves
                .Where(l => l.EmployeeId == leave.EmployeeId &&
                            l.Id != leave.Id &&
                            l.Status != "Rejected" &&
                            l.Status != "Cancelled" &&
                            l.StartDate.Date <= leave.EndDate.Date &&
                            l.EndDate.Date >= leave.StartDate.Date)
                .ToListAsync();

            foreach (var ol in overlappingLeaves)
            {
                if (!ol.IsHalfDay || !leave.IsHalfDay)
                {
                    throw new Exception($"You already have an active leave request ({ol.LeaveType}: {ol.StartDate:yyyy-MM-dd} to {ol.EndDate:yyyy-MM-dd}, Status: {ol.Status}) overlapping with the selected dates.");
                }

                if (ol.StartDate.Date == leave.StartDate.Date &&
                    string.Equals(ol.HalfDaySession, leave.HalfDaySession, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception($"You already have a {ol.HalfDaySession} leave request ({ol.LeaveType}, Status: {ol.Status}) on {leave.StartDate:yyyy-MM-dd}.");
                }
            }

            var entitlement = await GetLeaveBalanceAsync(leave.EmployeeId, leave.LeaveType, leave.StartDate.Year);
            if (entitlement.RemainingDays < leave.TotalDays)
            {
                throw new Exception($"Insufficient leave balance for {leave.LeaveType} Leave. Available: {entitlement.RemainingDays:0.#} day(s), Requested: {leave.TotalDays:0.#} day(s).");
            }

            var applicantRole = await GetApplicantWorkflowRoleAsync(employee);

            // Set starting status of workflow based on applicant role:
            // 1. Area Manager -> PendingHR
            // 2. Branch Manager -> PendingAM
            // 3. Department Head -> PendingBM
            // 4. Normal Employee -> PendingDH (or PendingBM if no department)
            if (applicantRole == "Area Manager")
            {
                leave.Status = "PendingHR";
            }
            else if (applicantRole == "Branch Manager")
            {
                leave.Status = "PendingAM";
            }
            else if (applicantRole == "Department Head")
            {
                leave.Status = "PendingBM";
            }
            else
            {
                leave.Status = (employee.DepartmentId == null || employee.DepartmentId <= 0) ? "PendingBM" : "PendingDH";
            }

            _context.Leaves.Add(leave);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Leave applied - EmployeeId: {EmployeeId}, Role: {Role}, Type: {Type}, Days: {Days}, IsHalfDay: {IsHalfDay}, Session: {Session}, Status: {Status}", 
                leave.EmployeeId, applicantRole, leave.LeaveType, leave.TotalDays, leave.IsHalfDay, leave.HalfDaySession, leave.Status);

            string durationText = leave.IsHalfDay ? $"0.5 days ({leave.HalfDaySession})" : $"{leave.TotalDays:0.#} days";

            // Send notification to the first required approver(s)
            try
            {
                if (leave.Status == "PendingHR")
                {
                    await NotifyHrManagersAsync("New Leave Request (HR Approval)",
                        $"{applicantRole} {employee.FullName} has requested {leave.LeaveType} Leave from {leave.StartDate:MMM dd, yyyy} to {leave.EndDate:MMM dd, yyyy} ({durationText}).", leave.Id, employee.Email);
                }
                else if (leave.Status == "PendingAM")
                {
                    await NotifyAreaManagersForBranchAsync(employee.BranchId, "New Leave Request (Area Manager Approval)",
                        $"{applicantRole} {employee.FullName} has requested {leave.LeaveType} Leave from {leave.StartDate:MMM dd, yyyy} to {leave.EndDate:MMM dd, yyyy} ({durationText}).", leave.Id, employee.Email);
                }
                else if (leave.Status == "PendingBM")
                {
                    await NotifyBranchManagersAsync(employee.BranchId, "New Leave Request (Branch Manager Approval)",
                        $"{applicantRole} {employee.FullName} has requested {leave.LeaveType} Leave from {leave.StartDate:MMM dd, yyyy} to {leave.EndDate:MMM dd, yyyy} ({durationText}).", leave.Id, employee.Email);
                }
                else
                {
                    await NotifyDepartmentHeadsAsync(employee.BranchId, employee.DepartmentId ?? 0, "New Leave Request (Department Head Approval)",
                        $"{employee.FullName} has requested {leave.LeaveType} Leave from {leave.StartDate:MMM dd, yyyy} to {leave.EndDate:MMM dd, yyyy} ({durationText}).", leave.Id, employee.Email);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notifications for leave request");
            }

            return leave;
        }

        public async Task<List<Leave>> GetEmployeeLeavesAsync(int employeeId)
        {
            return await _context.Leaves
                .Include(l => l.Employee)
                .Where(l => l.EmployeeId == employeeId)
                .OrderByDescending(l => l.AppliedDate)
                .ToListAsync();
        }

        public async Task<List<Leave>> GetPendingApprovalsAsync(int approverId)
        {
            var approverEmp = await _context.Employees
                .Include(e => e.Branch)
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.Id == approverId);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmployeeId == approverId || (approverEmp != null && u.Email == approverEmp.Email));
            if (user == null && approverEmp == null) return new List<Leave>();

            var userRoles = user != null
                ? await (from ur in _context.UserRoles
                         join r in _context.Roles on ur.RoleId equals r.Id
                         where ur.UserId == user.Id
                         select r.Name).ToListAsync()
                : new List<string>();

            // Approver Branch ID & Name
            int approverBranchId = approverEmp?.BranchId ?? 0;
            string? approverBranchName = user?.Branch ?? approverEmp?.Branch?.Name;
            if (approverBranchId == 0 && !string.IsNullOrEmpty(approverBranchName))
            {
                var b = await _context.Branches.FirstOrDefaultAsync(br => br.Name == approverBranchName);
                if (b != null) approverBranchId = b.Id;
            }

            // Approver Department ID & Name
            int approverDeptId = approverEmp?.DepartmentId ?? 0;
            string? approverDeptName = user?.Department ?? approverEmp?.Department?.Name;
            if (approverDeptId == 0 && !string.IsNullOrEmpty(approverDeptName))
            {
                var d = await _context.Departments.FirstOrDefaultAsync(dp => dp.Name == approverDeptName);
                if (d != null) approverDeptId = d.Id;
            }

            // Approver Managed Branch IDs (for Area Manager duty accounts)
            List<int> managedBranchIds = new();
            if (user != null && !string.IsNullOrEmpty(user.ManagedBranches))
            {
                managedBranchIds = user.ManagedBranches.Split(',')
                    .Select(s => int.TryParse(s.Trim(), out var bid) ? bid : 0)
                    .Where(bid => bid > 0)
                    .ToList();
            }

            var allPending = await _context.Leaves
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Branch)
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Department)
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Designation)
                .Include(l => l.MaternityLeave)
                .Include(l => l.OverseasLeave)
                .Where(l => l.Status == "Pending" || l.Status == "PendingDH" || l.Status == "PendingBM" || l.Status == "PendingAM" || l.Status == "PendingHR")
                .OrderBy(l => l.AppliedDate)
                .ToListAsync();

            var results = new List<Leave>();

            foreach (var leave in allPending)
            {
                if (userRoles.Contains("Admin"))
                {
                    results.Add(leave);
                    continue;
                }

                if (userRoles.Contains("Department Head") && (leave.Status == "Pending" || leave.Status == "PendingDH"))
                {
                    bool branchMatches = (approverBranchId > 0 && leave.Employee?.BranchId == approverBranchId) ||
                                         (!string.IsNullOrEmpty(approverBranchName) && leave.Employee?.Branch?.Name == approverBranchName);
                    bool deptMatches = (approverDeptId > 0 && leave.Employee?.DepartmentId == approverDeptId) ||
                                       (!string.IsNullOrEmpty(approverDeptName) && leave.Employee?.Department?.Name == approverDeptName);
                    if (branchMatches && deptMatches)
                    {
                        results.Add(leave);
                        continue;
                    }
                }

                if (userRoles.Contains("Branch Manager") && leave.Status == "PendingBM")
                {
                    bool branchMatches = (approverBranchId > 0 && leave.Employee?.BranchId == approverBranchId) ||
                                         (!string.IsNullOrEmpty(approverBranchName) && leave.Employee?.Branch?.Name == approverBranchName);
                    if (branchMatches)
                    {
                        results.Add(leave);
                        continue;
                    }
                }

                if (userRoles.Contains("Area Manager") && leave.Status == "PendingAM")
                {
                    bool isManaged = managedBranchIds.Any()
                        ? (leave.Employee?.BranchId != null && managedBranchIds.Contains(leave.Employee.BranchId))
                        : (approverBranchId == 0 || leave.Employee?.BranchId == approverBranchId || leave.Employee?.Branch?.Name == approverBranchName);
                    if (isManaged)
                    {
                        results.Add(leave);
                        continue;
                    }
                }

                if ((userRoles.Contains("HR Manager") || userRoles.Contains("HR Officer")) && leave.Status == "PendingHR")
                {
                    results.Add(leave);
                    continue;
                }
            }

            return results;
        }

        public async Task<Leave> ApproveLeaveAsync(int leaveId, int approverId, string comments)
        {
            var leave = await _context.Leaves
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Designation)
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Branch)
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Department)
                .Include(l => l.MaternityLeave)
                .Include(l => l.OverseasLeave)
                .Include(l => l.MaternityPayment)
                .FirstOrDefaultAsync(l => l.Id == leaveId);

            if (leave == null)
                throw new Exception("Leave not found");

            var approverEmp = await _context.Employees
                .Include(e => e.Branch)
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.Id == approverId);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmployeeId == approverId || (approverEmp != null && u.Email == approverEmp.Email));
            if (user == null && approverEmp == null)
                throw new Exception("Approver user account not found");

            var userRoles = user != null
                ? await (from ur in _context.UserRoles
                         join r in _context.Roles on ur.RoleId equals r.Id
                         where ur.UserId == user.Id
                         select r.Name).ToListAsync()
                : new List<string>();

            int approverBranchId = approverEmp?.BranchId ?? 0;
            string? approverBranchName = user?.Branch ?? approverEmp?.Branch?.Name;
            if (approverBranchId == 0 && !string.IsNullOrEmpty(approverBranchName))
            {
                var b = await _context.Branches.FirstOrDefaultAsync(br => br.Name == approverBranchName);
                if (b != null) approverBranchId = b.Id;
            }

            int approverDeptId = approverEmp?.DepartmentId ?? 0;
            string? approverDeptName = user?.Department ?? approverEmp?.Department?.Name;
            if (approverDeptId == 0 && !string.IsNullOrEmpty(approverDeptName))
            {
                var d = await _context.Departments.FirstOrDefaultAsync(dp => dp.Name == approverDeptName);
                if (d != null) approverDeptId = d.Id;
            }

            List<int> managedBranchIds = new();
            if (user != null && !string.IsNullOrEmpty(user.ManagedBranches))
            {
                managedBranchIds = user.ManagedBranches.Split(',')
                    .Select(s => int.TryParse(s.Trim(), out var bid) ? bid : 0)
                    .Where(bid => bid > 0)
                    .ToList();
            }

            var applicantRole = await GetApplicantWorkflowRoleAsync(leave.Employee);

            if (leave.Status == "Pending" || leave.Status == "PendingDH")
            {
                bool isAuthorized = userRoles.Contains("Admin") || 
                    (userRoles.Contains("Department Head") &&
                     ((approverBranchId > 0 && leave.Employee?.BranchId == approverBranchId) || (!string.IsNullOrEmpty(approverBranchName) && leave.Employee?.Branch?.Name == approverBranchName)) &&
                     ((approverDeptId > 0 && leave.Employee?.DepartmentId == approverDeptId) || (!string.IsNullOrEmpty(approverDeptName) && leave.Employee?.Department?.Name == approverDeptName)));

                if (!isAuthorized)
                    throw new Exception("You are not authorized to perform Department Head approval for this leave request.");

                leave.Status = "PendingBM";
                var approvalLog = new LeaveApproval
                {
                    EmployeeId = leave.EmployeeId,
                    LeaveId = leave.Id,
                    ApproverId = approverId,
                    Status = "Approved",
                    Comments = comments,
                    ApprovalDate = DateTime.Now
                };
                _context.LeaveApprovals.Add(approvalLog);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Department Head approved leave - LeaveId: {LeaveId}, ApproverId: {ApproverId}, NewStatus: {Status}", leaveId, approverId, leave.Status);

                if (leave.Employee != null)
                {
                    await NotifyBranchManagersAsync(leave.Employee.BranchId, $"New {leave.LeaveType} Leave Request (Branch Manager Approval)",
                        $"{leave.LeaveType} leave request from {leave.Employee.FullName} has been approved by Department Head and is pending your approval.", leave.Id, leave.Employee.Email);
                }
            }
            else if (leave.Status == "PendingBM")
            {
                bool isAuthorized = userRoles.Contains("Admin") || 
                    (userRoles.Contains("Branch Manager") &&
                     ((approverBranchId > 0 && leave.Employee?.BranchId == approverBranchId) || (!string.IsNullOrEmpty(approverBranchName) && leave.Employee?.Branch?.Name == approverBranchName)));

                if (!isAuthorized)
                    throw new Exception("You are not authorized to perform Branch Manager approval for this leave request.");

                if (leave.LeaveType == "Maternity" || leave.LeaveType == "Overseas")
                {
                    // Maternity and Overseas leave advance from Branch Manager to HR Officer for finalization!
                    leave.Status = "PendingHR";
                    if (leave.MaternityLeave != null)
                    {
                        leave.MaternityLeave.VerificationStatus = "BM Approved / Pending HR";
                    }
                    if (leave.OverseasLeave != null)
                    {
                        leave.OverseasLeave.VerificationStatus = "BM Approved / Pending HR";
                    }

                    var approvalLog = new LeaveApproval
                    {
                        EmployeeId = leave.EmployeeId,
                        LeaveId = leave.Id,
                        ApproverId = approverId,
                        Status = "Approved",
                        Comments = comments,
                        ApprovalDate = DateTime.Now
                    };
                    _context.LeaveApprovals.Add(approvalLog);
                    await _context.SaveChangesAsync();

                    if (leave.Employee != null)
                    {
                        await NotifyHrManagersAsync($"New {leave.LeaveType} Leave Request (Pending HR Finalization)",
                            $"{leave.LeaveType} leave request from {leave.Employee.FullName} has been approved by the Branch Manager and is pending your HR finalization.", leave.Id, leave.Employee.Email);
                    }
                }
                else if (applicantRole == "Department Head")
                {
                    // Department Head leave progresses to Area Manager
                    leave.Status = "PendingAM";
                    var approvalLog = new LeaveApproval
                    {
                        EmployeeId = leave.EmployeeId,
                        LeaveId = leave.Id,
                        ApproverId = approverId,
                        Status = "Approved",
                        Comments = comments,
                        ApprovalDate = DateTime.Now
                    };
                    _context.LeaveApprovals.Add(approvalLog);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Branch Manager approved DH leave - LeaveId: {LeaveId}, ApproverId: {ApproverId}, NewStatus: {Status}", leaveId, approverId, leave.Status);

                    if (leave.Employee != null)
                    {
                        await NotifyAreaManagersForBranchAsync(leave.Employee.BranchId, $"New {leave.LeaveType} Leave Request (Area Manager Approval)",
                            $"Department Head {leave.Employee.FullName} has requested {leave.LeaveType} Leave and has been approved by the Branch Manager. Pending your approval.", leave.Id, leave.Employee.Email);
                    }
                }
                else
                {
                    // Normal Employee: Branch Manager approval is the final approval!
                    leave.Status = "Approved";
                    leave.ApprovedById = approverId;
                    leave.ApprovedDate = DateTime.Now;

                    var entitlement = await GetLeaveBalanceAsync(leave.EmployeeId, leave.LeaveType, leave.StartDate.Year);
                    entitlement.UsedDays += leave.TotalDays;
                    entitlement.RemainingDays = entitlement.TotalDays - entitlement.UsedDays;

                    var approvalLog = new LeaveApproval
                    {
                        EmployeeId = leave.EmployeeId,
                        LeaveId = leave.Id,
                        ApproverId = approverId,
                        Status = "Approved",
                        Comments = comments,
                        ApprovalDate = DateTime.Now
                    };
                    _context.LeaveApprovals.Add(approvalLog);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Leave fully approved by BM - LeaveId: {LeaveId}, ApproverId: {ApproverId}", leaveId, approverId);

                    await SendApprovalNotificationToEmployeeAsync(leave, "Branch Manager");
                }
            }
            else if (leave.Status == "PendingAM")
            {
                bool isAuthorized = userRoles.Contains("Admin") ||
                    (userRoles.Contains("Area Manager") &&
                     (managedBranchIds.Any() ? (leave.Employee?.BranchId != null && managedBranchIds.Contains(leave.Employee.BranchId)) : (approverBranchId == 0 || leave.Employee?.BranchId == approverBranchId || leave.Employee?.Branch?.Name == approverBranchName)));

                if (!isAuthorized)
                    throw new Exception("You are not authorized to perform Area Manager approval for this leave request.");

                if (applicantRole == "Branch Manager")
                {
                    // Branch Manager leave progresses to HR Manager
                    leave.Status = "PendingHR";
                    var approvalLog = new LeaveApproval
                    {
                        EmployeeId = leave.EmployeeId,
                        LeaveId = leave.Id,
                        ApproverId = approverId,
                        Status = "Approved",
                        Comments = comments,
                        ApprovalDate = DateTime.Now
                    };
                    _context.LeaveApprovals.Add(approvalLog);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Area Manager approved BM leave - LeaveId: {LeaveId}, ApproverId: {ApproverId}, NewStatus: {Status}", leaveId, approverId, leave.Status);

                    if (leave.Employee != null)
                    {
                        await NotifyHrManagersAsync($"New {leave.LeaveType} Leave Request (HR Final Approval)",
                            $"Branch Manager {leave.Employee.FullName} has requested {leave.LeaveType} Leave and has been approved by the Area Manager. Pending your final approval.", leave.Id, leave.Employee.Email);
                    }
                }
                else
                {
                    // Department Head: Area Manager approval is the final approval!
                    leave.Status = "Approved";
                    leave.ApprovedById = approverId;
                    leave.ApprovedDate = DateTime.Now;

                    var entitlement = await GetLeaveBalanceAsync(leave.EmployeeId, leave.LeaveType, leave.StartDate.Year);
                    entitlement.UsedDays += leave.TotalDays;
                    entitlement.RemainingDays = entitlement.TotalDays - entitlement.UsedDays;

                    var approvalLog = new LeaveApproval
                    {
                        EmployeeId = leave.EmployeeId,
                        LeaveId = leave.Id,
                        ApproverId = approverId,
                        Status = "Approved",
                        Comments = comments,
                        ApprovalDate = DateTime.Now
                    };
                    _context.LeaveApprovals.Add(approvalLog);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Leave fully approved by AM - LeaveId: {LeaveId}, ApproverId: {ApproverId}", leaveId, approverId);

                    await SendApprovalNotificationToEmployeeAsync(leave, "Area Manager");
                }
            }
            else if (leave.Status == "PendingHR")
            {
                bool isAuthorized = userRoles.Contains("Admin") || userRoles.Contains("HR Manager") || userRoles.Contains("HR Officer");

                if (!isAuthorized)
                    throw new Exception("You are not authorized to finalize this leave request as HR.");

                // HR Officer / Manager approval is final!
                leave.Status = "Approved";
                leave.ApprovedById = approverId;
                leave.ApprovedDate = DateTime.Now;

                if (leave.MaternityLeave != null)
                {
                    leave.MaternityLeave.VerificationStatus = "Approved";
                    if (leave.MaternityPayment == null)
                    {
                        leave.MaternityPayment = new MaternityPayment
                        {
                            LeaveId = leave.Id,
                            SalaryAdjustmentType = "Full Pay",
                            SalaryPercentage = 100,
                            Status = "Pending Processing"
                        };
                        _context.MaternityPayments.Add(leave.MaternityPayment);
                    }
                }

                if (leave.OverseasLeave != null)
                {
                    leave.OverseasLeave.VerificationStatus = "Approved";
                    leave.OverseasLeave.BoardApprovalStatus = "Approved";
                }

                var entitlement = await GetLeaveBalanceAsync(leave.EmployeeId, leave.LeaveType, leave.StartDate.Year);
                entitlement.UsedDays += leave.TotalDays;
                entitlement.RemainingDays = entitlement.TotalDays - entitlement.UsedDays;

                var approvalLog = new LeaveApproval
                {
                    EmployeeId = leave.EmployeeId,
                    LeaveId = leave.Id,
                    ApproverId = approverId,
                    Status = "Approved",
                    Comments = comments,
                    ApprovalDate = DateTime.Now
                };
                _context.LeaveApprovals.Add(approvalLog);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Leave fully finalized by HR - LeaveId: {LeaveId}, ApproverId: {ApproverId}", leaveId, approverId);

                string actorTitle = userRoles.Contains("HR Officer") ? "HR Officer" : "HR Manager";
                await SendApprovalNotificationToEmployeeAsync(leave, actorTitle);
            }
            else
            {
                throw new Exception("Leave request is not in a status that can be approved.");
            }

            return leave;
        }

        public async Task<Leave> RejectLeaveAsync(int leaveId, int approverId, string reason)
        {
            var leave = await _context.Leaves
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Branch)
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Department)
                .Include(l => l.MaternityLeave)
                .Include(l => l.OverseasLeave)
                .FirstOrDefaultAsync(l => l.Id == leaveId);

            if (leave == null)
                throw new Exception("Leave not found");

            var approverEmp = await _context.Employees
                .Include(e => e.Branch)
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.Id == approverId);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmployeeId == approverId || (approverEmp != null && u.Email == approverEmp.Email));
            if (user == null && approverEmp == null)
                throw new Exception("Approver user account not found");

            var userRoles = user != null
                ? await (from ur in _context.UserRoles
                         join r in _context.Roles on ur.RoleId equals r.Id
                         where ur.UserId == user.Id
                         select r.Name).ToListAsync()
                : new List<string>();

            int approverBranchId = approverEmp?.BranchId ?? 0;
            string? approverBranchName = user?.Branch ?? approverEmp?.Branch?.Name;
            if (approverBranchId == 0 && !string.IsNullOrEmpty(approverBranchName))
            {
                var b = await _context.Branches.FirstOrDefaultAsync(br => br.Name == approverBranchName);
                if (b != null) approverBranchId = b.Id;
            }

            int approverDeptId = approverEmp?.DepartmentId ?? 0;
            string? approverDeptName = user?.Department ?? approverEmp?.Department?.Name;
            if (approverDeptId == 0 && !string.IsNullOrEmpty(approverDeptName))
            {
                var d = await _context.Departments.FirstOrDefaultAsync(dp => dp.Name == approverDeptName);
                if (d != null) approverDeptId = d.Id;
            }

            List<int> managedBranchIds = new();
            if (user != null && !string.IsNullOrEmpty(user.ManagedBranches))
            {
                managedBranchIds = user.ManagedBranches.Split(',')
                    .Select(s => int.TryParse(s.Trim(), out var bid) ? bid : 0)
                    .Where(bid => bid > 0)
                    .ToList();
            }

            bool isAuthorized = userRoles.Contains("Admin");
            string actorTitle = "Management";

            if (leave.Status == "Pending" || leave.Status == "PendingDH")
            {
                isAuthorized = isAuthorized || (userRoles.Contains("Department Head") &&
                    ((approverBranchId > 0 && leave.Employee?.BranchId == approverBranchId) || (!string.IsNullOrEmpty(approverBranchName) && leave.Employee?.Branch?.Name == approverBranchName)) &&
                    ((approverDeptId > 0 && leave.Employee?.DepartmentId == approverDeptId) || (!string.IsNullOrEmpty(approverDeptName) && leave.Employee?.Department?.Name == approverDeptName)));
                actorTitle = "Department Head";
            }
            else if (leave.Status == "PendingBM")
            {
                isAuthorized = isAuthorized || (userRoles.Contains("Branch Manager") &&
                    ((approverBranchId > 0 && leave.Employee?.BranchId == approverBranchId) || (!string.IsNullOrEmpty(approverBranchName) && leave.Employee?.Branch?.Name == approverBranchName)));
                actorTitle = "Branch Manager";
            }
            else if (leave.Status == "PendingAM")
            {
                isAuthorized = isAuthorized || (userRoles.Contains("Area Manager") &&
                    (managedBranchIds.Any() ? managedBranchIds.Contains(leave.Employee.BranchId) : (approverBranchId == 0 || leave.Employee.BranchId == approverBranchId || leave.Employee?.Branch?.Name == approverBranchName)));
                actorTitle = "Area Manager";
            }
            else if (leave.Status == "PendingHR")
            {
                isAuthorized = isAuthorized || userRoles.Contains("HR Manager") || userRoles.Contains("HR Officer");
                actorTitle = userRoles.Contains("HR Officer") ? "HR Officer" : "HR Manager";
            }

            if (!isAuthorized)
                throw new Exception("You are not authorized to reject this leave request at this stage of approval.");

            leave.Status = "Rejected";
            leave.ApprovedById = approverId;
            leave.ApprovedDate = DateTime.Now;
            leave.RejectionReason = reason;

            if (leave.MaternityLeave != null)
            {
                leave.MaternityLeave.VerificationStatus = "Rejected";
                leave.MaternityLeave.VerificationComments = reason;
            }
            if (leave.OverseasLeave != null)
            {
                leave.OverseasLeave.VerificationStatus = "Rejected";
                leave.OverseasLeave.VerificationComments = reason;
            }

            var approvalLog = new LeaveApproval
            {
                EmployeeId = leave.EmployeeId,
                LeaveId = leave.Id,
                ApproverId = approverId,
                Status = "Rejected",
                Comments = reason,
                ApprovalDate = DateTime.Now
            };
            _context.LeaveApprovals.Add(approvalLog);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Leave rejected - LeaveId: {LeaveId}, Reason: {Reason}", leaveId, reason);

            try
            {
                if (leave.Employee != null && !string.IsNullOrEmpty(leave.Employee.Email))
                {
                    await _notificationService.CreateNotificationAsync(
                        leave.Employee.Email,
                        "Leave Rejected",
                        $"Your leave request from {leave.StartDate:MMM dd, yyyy} to {leave.EndDate:MMM dd, yyyy} was rejected by {actorTitle}. Reason: {reason}",
                        HRMS.Domain.Entities.Core.CoreNotificationType.Rejected,
                        "/Employee/Leave/Status"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send rejection notification");
            }

            return leave;
        }

        private async Task SendApprovalNotificationToEmployeeAsync(Leave leave, string approvedByTitle)
        {
            try
            {
                if (leave.Employee != null && !string.IsNullOrEmpty(leave.Employee.Email))
                {
                    await _notificationService.CreateNotificationAsync(
                        leave.Employee.Email,
                        "Leave Approved",
                        $"Your leave request from {leave.StartDate:MMM dd, yyyy} to {leave.EndDate:MMM dd, yyyy} ({leave.TotalDays} days) has been fully approved by the {approvedByTitle}.",
                        HRMS.Domain.Entities.Core.CoreNotificationType.Approved,
                        "/Employee/Leave/Status"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send leave approval notification");
            }
        }

        private async Task NotifyDepartmentHeadsAsync(int branchId, int departmentId, string title, string message, int leaveId, string? excludeEmail = null)
        {
            try
            {
                var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Department Head");
                if (role == null) return;

                var branch = await _context.Branches.FindAsync(branchId);
                var dept = await _context.Departments.FindAsync(departmentId);

                var dhUsers = await (from ur in _context.UserRoles
                                     join u in _context.Users on ur.UserId equals u.Id
                                     where ur.RoleId == role.Id
                                     select u).ToListAsync();

                var targetEmails = new List<string>();
                foreach (var u in dhUsers)
                {
                    if (string.IsNullOrEmpty(u.Email)) continue;
                    if (!string.IsNullOrEmpty(excludeEmail) && u.Email.Equals(excludeEmail, StringComparison.OrdinalIgnoreCase)) continue;

                    // Match branch & department
                    bool branchMatches = (!string.IsNullOrEmpty(u.Branch) && (u.Branch.Equals(branch?.Name, StringComparison.OrdinalIgnoreCase) || u.Branch == branchId.ToString()));
                    bool deptMatches = (!string.IsNullOrEmpty(u.Department) && (u.Department.Equals(dept?.Name, StringComparison.OrdinalIgnoreCase) || u.Department == departmentId.ToString()));

                    if (branchMatches && deptMatches)
                    {
                        targetEmails.Add(u.Email);
                    }
                    else if (u.EmployeeId.HasValue)
                    {
                        var emp = await _context.Employees.FindAsync(u.EmployeeId.Value);
                        if (emp != null && !emp.NIC.StartsWith("DUTY") && emp.NIC != "DUTY-ACC" && emp.BranchId == branchId && emp.DepartmentId == departmentId)
                        {
                            targetEmails.Add(u.Email);
                        }
                    }
                }

                foreach (var email in targetEmails.Distinct())
                {
                    await _notificationService.CreateNotificationAsync(
                        email,
                        title,
                        message,
                        HRMS.Domain.Entities.Core.CoreNotificationType.Info,
                        $"/Manager/Leave/Review?id={leaveId}"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notifications to Department Heads");
            }
        }

        private async Task NotifyBranchManagersAsync(int branchId, string title, string message, int leaveId, string? excludeEmail = null)
        {
            try
            {
                var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Branch Manager");
                if (role == null) return;

                var branch = await _context.Branches.FindAsync(branchId);

                var bmUsers = await (from ur in _context.UserRoles
                                     join u in _context.Users on ur.UserId equals u.Id
                                     where ur.RoleId == role.Id
                                     select u).ToListAsync();

                var targetEmails = new List<string>();
                foreach (var u in bmUsers)
                {
                    if (string.IsNullOrEmpty(u.Email)) continue;
                    if (!string.IsNullOrEmpty(excludeEmail) && u.Email.Equals(excludeEmail, StringComparison.OrdinalIgnoreCase)) continue;

                    bool branchMatches = (!string.IsNullOrEmpty(u.Branch) && (u.Branch.Equals(branch?.Name, StringComparison.OrdinalIgnoreCase) || u.Branch == branchId.ToString()));

                    if (branchMatches)
                    {
                        targetEmails.Add(u.Email);
                    }
                    else if (u.EmployeeId.HasValue)
                    {
                        var emp = await _context.Employees.FindAsync(u.EmployeeId.Value);
                        if (emp != null && !emp.NIC.StartsWith("DUTY") && emp.NIC != "DUTY-ACC" && emp.BranchId == branchId)
                        {
                            targetEmails.Add(u.Email);
                        }
                    }
                }

                foreach (var email in targetEmails.Distinct())
                {
                    await _notificationService.CreateNotificationAsync(
                        email,
                        title,
                        message,
                        HRMS.Domain.Entities.Core.CoreNotificationType.Info,
                        $"/Manager/Leave/Review?id={leaveId}"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notifications to Branch Managers in branch {BranchId}", branchId);
            }
        }

        private async Task NotifyAreaManagersForBranchAsync(int branchId, string title, string message, int leaveId, string? excludeEmail = null)
        {
            try
            {
                var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Area Manager");
                if (role == null) return;

                var branch = await _context.Branches.FindAsync(branchId);

                var amUsers = await (from ur in _context.UserRoles
                                     join u in _context.Users on ur.UserId equals u.Id
                                     where ur.RoleId == role.Id
                                     select u).ToListAsync();

                var targetEmails = new List<string>();
                foreach (var u in amUsers)
                {
                    if (string.IsNullOrEmpty(u.Email)) continue;
                    if (!string.IsNullOrEmpty(excludeEmail) && u.Email.Equals(excludeEmail, StringComparison.OrdinalIgnoreCase)) continue;

                    if (!string.IsNullOrEmpty(u.ManagedBranches))
                    {
                        var branchIds = u.ManagedBranches.Split(',')
                            .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                            .Where(id => id > 0);
                        if (branchIds.Contains(branchId))
                        {
                            targetEmails.Add(u.Email);
                        }
                    }
                    else if (!string.IsNullOrEmpty(u.Branch) && (u.Branch.Equals(branch?.Name, StringComparison.OrdinalIgnoreCase) || u.Branch == branchId.ToString()))
                    {
                        targetEmails.Add(u.Email);
                    }
                }

                foreach (var email in targetEmails.Distinct())
                {
                    await _notificationService.CreateNotificationAsync(
                        email,
                        title,
                        message,
                        HRMS.Domain.Entities.Core.CoreNotificationType.Info,
                        $"/Manager/Leave/Review?id={leaveId}"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notifications to Area Managers for branch {BranchId}", branchId);
            }
        }

        private async Task NotifyHrManagersAsync(string title, string message, int leaveId, string? excludeEmail = null)
        {
            try
            {
                var roleNames = new[] { "HR Manager", "HR Officer" };
                var hrRoles = await _context.Roles.Where(r => roleNames.Contains(r.Name)).Select(r => r.Id).ToListAsync();
                if (!hrRoles.Any()) return;

                var hrUsers = await (from ur in _context.UserRoles
                                     join u in _context.Users on ur.UserId equals u.Id
                                     where hrRoles.Contains(ur.RoleId) && !string.IsNullOrEmpty(u.Email)
                                     select u).ToListAsync();

                var targetEmails = new List<string>();
                foreach (var u in hrUsers)
                {
                    if (string.IsNullOrEmpty(u.Email)) continue;
                    if (!string.IsNullOrEmpty(excludeEmail) && u.Email.Equals(excludeEmail, StringComparison.OrdinalIgnoreCase)) continue;

                    targetEmails.Add(u.Email);
                }

                foreach (var email in targetEmails.Distinct())
                {
                    await _notificationService.CreateNotificationAsync(
                        email,
                        title,
                        message,
                        HRMS.Domain.Entities.Core.CoreNotificationType.Info,
                        $"/Manager/Leave/Review?id={leaveId}"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notifications to HR Managers");
            }
        }

        public async Task<double> CalculateLeaveDaysAsync(DateTime startDate, DateTime endDate)
        {
            double days = 0;
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                    days++;
            }
            return days;
        }

        public async Task<bool> HasEnoughBalanceAsync(int employeeId, string leaveType, double days)
        {
            var entitlement = await GetLeaveBalanceAsync(employeeId, leaveType, DateTime.Now.Year);
            return entitlement.RemainingDays >= days;
        }

        private string NormalizeEmployeeType(string? empType)
        {
            if (string.IsNullOrWhiteSpace(empType)) return "Permanent";
            if (empType.Equals("Intern", StringComparison.OrdinalIgnoreCase)) return "Intern";
            if (empType.StartsWith("Probation", StringComparison.OrdinalIgnoreCase)) return "Probationary";
            return "Permanent";
        }

        private async Task<int> GetDefaultLeaveDaysAsync(string leaveType, string employeeType = "Permanent")
        {
            employeeType = NormalizeEmployeeType(employeeType);
            var connection = _context.Database.GetDbConnection();
            bool openedLocally = false;
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
                openedLocally = true;
            }

            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT DefaultDays FROM LeaveAllocationSettings WHERE LeaveType = @leaveType AND EmployeeType = @empType ORDER BY Id DESC LIMIT 1";
                    
                    var pType = cmd.CreateParameter();
                    pType.ParameterName = "@leaveType";
                    pType.Value = leaveType;
                    cmd.Parameters.Add(pType);

                    var pEmp = cmd.CreateParameter();
                    pEmp.ParameterName = "@empType";
                    pEmp.Value = employeeType;
                    cmd.Parameters.Add(pEmp);

                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        return Convert.ToInt32(result);
                    }
                }

                // Fallback to defaults by employee type
                int defaultDays = (employeeType, leaveType) switch
                {
                    ("Intern", "Annual")      => 0,
                    ("Intern", "Casual")      => 3,
                    ("Intern", "Medical")     => 5,
                    ("Intern", "Maternity")   => 0,
                    ("Intern", "Overseas")    => 0,
                    ("Intern", "Exam")        => 5,
                    ("Intern", "Bereavement") => 3,
                    ("Intern", "Other")       => 0,

                    ("Probationary", "Annual")      => 0,
                    ("Probationary", "Casual")      => 7,
                    ("Probationary", "Medical")     => 7,
                    ("Probationary", "Maternity")   => 84,
                    ("Probationary", "Overseas")    => 0,
                    ("Probationary", "Exam")        => 3,
                    ("Probationary", "Bereavement") => 3,
                    ("Probationary", "Other")       => 0,

                    // Permanent (default)
                    (_, "Annual")      => 14,
                    (_, "Casual")      => 7,
                    (_, "Medical")     => 14,
                    (_, "Maternity")   => 84,
                    (_, "Overseas")    => 30,
                    (_, "Exam")        => 7,
                    (_, "Bereavement") => 5,
                    (_, "Other")       => 0,
                    _                  => 0
                };

                // Seed it into the table so it's visible in settings
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "INSERT IGNORE INTO LeaveAllocationSettings (EmployeeType, LeaveType, DefaultDays) VALUES (@empType, @leaveType, @defaultDays)";
                    
                    var pEmp = cmd.CreateParameter();
                    pEmp.ParameterName = "@empType";
                    pEmp.Value = employeeType;
                    cmd.Parameters.Add(pEmp);

                    var pType = cmd.CreateParameter();
                    pType.ParameterName = "@leaveType";
                    pType.Value = leaveType;
                    cmd.Parameters.Add(pType);

                    var pDays = cmd.CreateParameter();
                    pDays.ParameterName = "@defaultDays";
                    pDays.Value = defaultDays;
                    cmd.Parameters.Add(pDays);

                    await cmd.ExecuteNonQueryAsync();
                }

                return defaultDays;
            }
            finally
            {
                if (openedLocally)
                {
                    await connection.CloseAsync();
                }
            }
        }
    }
}
