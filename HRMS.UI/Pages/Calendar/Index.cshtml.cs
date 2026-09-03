using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.Domain.Entities.Calendar;
using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Training;
using HRMS.Domain.Common;

namespace HRMS.UI.Pages.Calendar
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // View State
        public string CurrentView { get; set; } = "month"; // "month", "week", "day"
        public DateTime CurrentDate { get; set; } = SriLankaTime.Today;
        public string? SelectedCategory { get; set; }
        public int? SelectedBranchId { get; set; }

        // Data Lists
        public List<CalendarDisplayItem> Events { get; set; } = new();
        public List<Branch> Branches { get; set; } = new();
        public List<Department> Departments { get; set; } = new();

        // Month View Specifics
        public List<CalendarDayCell> MonthDays { get; set; } = new();

        // Week View Specifics
        public List<DateTime> WeekDays { get; set; } = new();

        // Stats / KPI summary
        public int TotalEventsThisMonth { get; set; }
        public int UpcomingTrainingsCount { get; set; }
        public int UpcomingMeetingsCount { get; set; }
        public int PersonalEventsCount { get; set; }

        public HRMS.Domain.Entities.Core.Employee? CurrentEmployee { get; set; }
        public bool CanCreateCompanyEvents { get; set; }

        // Form Binding for Create/Edit Modal
        [BindProperty]
        public EventInputModel Input { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(string? view, string? date, string? category, int? branchId)
        {
            CurrentView = (view ?? "month").ToLowerInvariant();
            if (!new[] { "month", "week", "day" }.Contains(CurrentView))
                CurrentView = "month";

            if (DateTime.TryParse(date, out var parsedDate))
                CurrentDate = parsedDate;
            else
                CurrentDate = SriLankaTime.Today;

            SelectedCategory = category;
            SelectedBranchId = branchId;

            var user = await _userManager.GetUserAsync(User);
            if (user != null && !string.IsNullOrWhiteSpace(user.Email))
            {
                CurrentEmployee = await _context.Employees
                    .Include(e => e.Branch)
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.Email == user.Email);
            }

            CanCreateCompanyEvents = User.IsInRole("Admin") || User.IsInRole("HR Manager") || User.IsInRole("HR Officer");

            Branches = await _context.Branches.OrderBy(b => b.Name).ToListAsync();
            Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();

            // Load unified events scoped by user role
            await LoadScopedEventsAsync(user);

            // Compute Month / Week / Day view data structures
            BuildCalendarViewData();

            return Page();
        }

        private async Task LoadScopedEventsAsync(ApplicationUser? user)
        {
            var userId = user?.Id ?? "";
            var userEmail = user?.Email ?? "";
            var empId = CurrentEmployee?.Id ?? 0;
            var empBranchId = CurrentEmployee?.BranchId;
            var empDeptId = CurrentEmployee?.DepartmentId;

            // 1. Fetch CalendarEvents (Strictly visible only to the user who added/created them)
            var eventQuery = _context.CalendarEvents
                .Include(e => e.Employee)
                .Include(e => e.Branch)
                .Include(e => e.Department)
                .Where(e => e.CreatedByUserId == userId)
                .AsQueryable();

            // 2. Fetch Trainings (Corporate / Branch training sessions)
            var trainingQuery = _context.Trainings
                .Include(t => t.EmployeeTrainings)
                    .ThenInclude(et => et.Employee)
                .AsQueryable();

            // Scope Trainings based on Role
            if (User.IsInRole("Admin") || User.IsInRole("HR Manager"))
            {
                if (SelectedBranchId.HasValue && SelectedBranchId > 0)
                {
                    trainingQuery = trainingQuery.Where(t => t.EmployeeTrainings.Any(et => et.Employee.BranchId == SelectedBranchId.Value));
                }
            }
            else if (User.IsInRole("HR Officer") || User.IsInRole("Area Manager"))
            {
                List<int> managedBranchIds = new();
                if (!string.IsNullOrWhiteSpace(user?.ManagedBranches))
                {
                    managedBranchIds = user.ManagedBranches
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s.Trim(), out var bid) ? bid : 0)
                        .Where(bid => bid > 0)
                        .ToList();
                }

                if (managedBranchIds.Count > 0)
                {
                    trainingQuery = trainingQuery.Where(t => t.EmployeeTrainings.Any(et => managedBranchIds.Contains(et.Employee.BranchId)));
                }
                else if (empBranchId.HasValue)
                {
                    trainingQuery = trainingQuery.Where(t => t.EmployeeTrainings.Any(et => et.Employee.BranchId == empBranchId.Value));
                }
            }
            else if (User.IsInRole("Branch Manager"))
            {
                int? bmBranchId = null;
                if (!string.IsNullOrWhiteSpace(user?.Branch))
                {
                    var b = await _context.Branches.FirstOrDefaultAsync(x => x.Name == user.Branch);
                    bmBranchId = b?.Id;
                }
                bmBranchId ??= empBranchId;

                if (bmBranchId.HasValue)
                {
                    trainingQuery = trainingQuery.Where(t => t.EmployeeTrainings.Any(et => et.Employee.BranchId == bmBranchId.Value));
                }
            }
            else if (User.IsInRole("Department Head"))
            {
                if (empDeptId.HasValue)
                {
                    trainingQuery = trainingQuery.Where(t => t.EmployeeTrainings.Any(et => et.Employee.DepartmentId == empDeptId.Value));
                }
            }
            else
            {
                // General Employee: Only see Training sessions if they are enrolled in the program
                trainingQuery = trainingQuery.Where(t => t.EmployeeTrainings.Any(et => et.EmployeeId == empId));
            }

            var rawEvents = await eventQuery.ToListAsync();
            var rawTrainings = await trainingQuery.ToListAsync();

            var combinedList = new List<CalendarDisplayItem>();

            // Map CalendarEvents
            foreach (var evt in rawEvents)
            {
                bool canEdit = (evt.CreatedByUserId == userId) || User.IsInRole("Admin") || User.IsInRole("HR Manager");
                combinedList.Add(new CalendarDisplayItem
                {
                    Id = evt.Id,
                    Title = evt.Title,
                    Description = evt.Description ?? "",
                    Category = evt.EventType,
                    CategoryColor = GetCategoryColor(evt.EventType),
                    StartTime = evt.StartTime,
                    EndTime = evt.EndTime,
                    IsAllDay = evt.IsAllDay,
                    Location = evt.Location ?? "",
                    MeetingLink = evt.MeetingLink ?? "",
                    IsTraining = false,
                    CanEdit = canEdit,
                    CanDelete = canEdit,
                    CreatorName = evt.Employee?.NameWithInitials ?? (evt.CreatedByUserId == userId ? "Me" : "HR Team"),
                    BranchName = evt.Branch?.Name ?? "All Branches",
                    DepartmentName = evt.Department?.Name ?? "All Departments"
                });
            }

            // Map Trainings
            foreach (var tr in rawTrainings)
            {
                var trStart = tr.Date.Date.Add(tr.StartTime);
                var trEnd = trStart.AddHours(tr.DurationHours > 0 ? tr.DurationHours : 2);
                var enrolledEmployees = tr.EmployeeTrainings.Select(et => et.Employee?.NameWithInitials ?? "Staff").ToList();

                combinedList.Add(new CalendarDisplayItem
                {
                    Id = tr.Id,
                    Title = $"[Training] {tr.Title}",
                    Description = tr.Description ?? "Corporate Training Session",
                    Category = "Training",
                    CategoryColor = "#10823c",
                    StartTime = trStart,
                    EndTime = trEnd,
                    IsAllDay = false,
                    Location = tr.Location ?? "Head Office Training Hall",
                    MeetingLink = "",
                    TrainerName = tr.TrainerName ?? tr.Trainer?.Name ?? "Internal Trainer",
                    AttendeeCount = tr.EmployeeTrainings.Count,
                    Attendees = enrolledEmployees,
                    IsTraining = true,
                    CanEdit = false, // Managed from Training module
                    CanDelete = false,
                    CreatorName = "Training Department",
                    BranchName = "Assigned Branches",
                    DepartmentName = "All Departments"
                });
            }

            // Filter by Category if selected
            if (!string.IsNullOrWhiteSpace(SelectedCategory) && SelectedCategory != "All")
            {
                combinedList = combinedList.Where(e => e.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            Events = combinedList.OrderBy(e => e.StartTime).ToList();

            // Compute KPIs for current month
            var monthStart = new DateTime(CurrentDate.Year, CurrentDate.Month, 1);
            var monthEnd = monthStart.AddMonths(1);
            var monthEvents = Events.Where(e => e.StartTime >= monthStart && e.StartTime < monthEnd).ToList();

            TotalEventsThisMonth = monthEvents.Count;
            UpcomingTrainingsCount = monthEvents.Count(e => e.Category == "Training");
            UpcomingMeetingsCount = monthEvents.Count(e => e.Category == "Meeting");
            PersonalEventsCount = monthEvents.Count(e => e.Category == "Personal" || e.Category == "Reminder");
        }

        private void BuildCalendarViewData()
        {
            // ── 1. Month View Grid Calculation ──
            MonthDays = new List<CalendarDayCell>();
            var firstDayOfMonth = new DateTime(CurrentDate.Year, CurrentDate.Month, 1);
            int daysInMonth = DateTime.DaysInMonth(CurrentDate.Year, CurrentDate.Month);

            // DayOfWeek for first day (0=Sunday, 1=Monday... let's use Monday as start of week)
            int startOffset = ((int)firstDayOfMonth.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;

            // Previous Month padding days
            var prevMonth = firstDayOfMonth.AddMonths(-1);
            int prevMonthDays = DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);
            for (int i = startOffset - 1; i >= 0; i--)
            {
                var d = new DateTime(prevMonth.Year, prevMonth.Month, prevMonthDays - i);
                MonthDays.Add(new CalendarDayCell
                {
                    Date = d,
                    IsCurrentMonth = false,
                    IsToday = (d.Date == SriLankaTime.Today),
                    DayNumber = d.Day,
                    Events = Events.Where(e => e.StartTime.Date == d.Date).ToList()
                });
            }

            // Current Month days
            for (int day = 1; day <= daysInMonth; day++)
            {
                var d = new DateTime(CurrentDate.Year, CurrentDate.Month, day);
                MonthDays.Add(new CalendarDayCell
                {
                    Date = d,
                    IsCurrentMonth = true,
                    IsToday = (d.Date == SriLankaTime.Today),
                    DayNumber = day,
                    Events = Events.Where(e => e.StartTime.Date == d.Date).ToList()
                });
            }

            // Next Month padding days to complete 35 or 42 grid cells
            int totalCells = MonthDays.Count <= 35 ? 35 : 42;
            int nextMonthDay = 1;
            var nextMonth = firstDayOfMonth.AddMonths(1);
            while (MonthDays.Count < totalCells)
            {
                var d = new DateTime(nextMonth.Year, nextMonth.Month, nextMonthDay++);
                MonthDays.Add(new CalendarDayCell
                {
                    Date = d,
                    IsCurrentMonth = false,
                    IsToday = (d.Date == SriLankaTime.Today),
                    DayNumber = d.Day,
                    Events = Events.Where(e => e.StartTime.Date == d.Date).ToList()
                });
            }

            // ── 2. Week View Calculation (Monday to Sunday) ──
            WeekDays = new List<DateTime>();
            int currentDayOfWeekOffset = ((int)CurrentDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var weekStart = CurrentDate.Date.AddDays(-currentDayOfWeekOffset);
            for (int i = 0; i < 7; i++)
            {
                WeekDays.Add(weekStart.AddDays(i));
            }
        }

        private string GetCategoryColor(string type) => type switch
        {
            "Training" => "#10823c",
            "Meeting" => "#0284c7",
            "Personal" => "#d97706",
            "Reminder" => "#e11d48",
            "Holiday" => "#dc2626",
            "Event" => "#7c3aed",
            _ => "#10823c"
        };

        // ── Handler: Create Personal/Corporate Event ──
        public async Task<IActionResult> OnPostCreateEventAsync()
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please provide valid event information.";
                return RedirectToPage(new { view = CurrentView, date = CurrentDate.ToString("yyyy-MM-dd"), category = SelectedCategory, branchId = SelectedBranchId });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Email == user.Email);

            DateTime startDt = Input.IsAllDay
                ? Input.StartDate.Date
                : Input.StartDate.Date.Add(Input.StartTime);

            DateTime endDt = Input.IsAllDay
                ? Input.StartDate.Date.AddDays(1).AddSeconds(-1)
                : Input.StartDate.Date.Add(Input.EndTime);

            if (endDt <= startDt && !Input.IsAllDay)
            {
                endDt = startDt.AddHours(1);
            }

            var newEvent = new CalendarEvent
            {
                Title = Input.Title.Trim(),
                Description = Input.Description?.Trim(),
                EventType = Input.EventType,
                StartTime = startDt,
                EndTime = endDt,
                IsAllDay = Input.IsAllDay,
                Location = Input.Location?.Trim(),
                MeetingLink = Input.MeetingLink?.Trim(),
                CreatedByUserId = user.Id,
                EmployeeId = emp?.Id,
                BranchId = (CanCreateCompanyEvents && Input.BranchId.HasValue && Input.BranchId > 0) ? Input.BranchId : (Input.EventType == "Personal" ? emp?.BranchId : null),
                DepartmentId = (CanCreateCompanyEvents && Input.DepartmentId.HasValue && Input.DepartmentId > 0) ? Input.DepartmentId : (Input.EventType == "Personal" ? emp?.DepartmentId : null),
                CreatedAt = SriLankaTime.Now,
                DayBeforeNotificationSent = false,
                HourBeforeNotificationSent = false
            };

            _context.CalendarEvents.Add(newEvent);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Event '{newEvent.Title}' added successfully to your calendar.";
            return RedirectToPage(new { view = CurrentView, date = Input.StartDate.ToString("yyyy-MM-dd"), category = SelectedCategory, branchId = SelectedBranchId });
        }

        // ── Handler: Edit Personal/Corporate Event ──
        public async Task<IActionResult> OnPostEditEventAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var evt = await _context.CalendarEvents.FirstOrDefaultAsync(e => e.Id == Input.Id);
            if (evt == null)
            {
                TempData["ErrorMessage"] = "Event not found.";
                return RedirectToPage();
            }

            bool canEdit = (evt.CreatedByUserId == user.Id) || User.IsInRole("Admin") || User.IsInRole("HR Manager");
            if (!canEdit)
            {
                TempData["ErrorMessage"] = "You are not authorized to edit this event.";
                return RedirectToPage();
            }

            DateTime startDt = Input.IsAllDay ? Input.StartDate.Date : Input.StartDate.Date.Add(Input.StartTime);
            DateTime endDt = Input.IsAllDay ? Input.StartDate.Date.AddDays(1).AddSeconds(-1) : Input.StartDate.Date.Add(Input.EndTime);

            if (endDt <= startDt && !Input.IsAllDay) endDt = startDt.AddHours(1);

            evt.Title = Input.Title.Trim();
            evt.Description = Input.Description?.Trim();
            evt.EventType = Input.EventType;
            evt.StartTime = startDt;
            evt.EndTime = endDt;
            evt.IsAllDay = Input.IsAllDay;
            evt.Location = Input.Location?.Trim();
            evt.MeetingLink = Input.MeetingLink?.Trim();

            if (CanCreateCompanyEvents)
            {
                evt.BranchId = (Input.BranchId.HasValue && Input.BranchId > 0) ? Input.BranchId : null;
                evt.DepartmentId = (Input.DepartmentId.HasValue && Input.DepartmentId > 0) ? Input.DepartmentId : null;
            }

            // Reset notification tracking if time changed significantly into future
            if (startDt > DateTime.Now.AddHours(25))
            {
                evt.DayBeforeNotificationSent = false;
                evt.HourBeforeNotificationSent = false;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Event '{evt.Title}' updated successfully.";
            return RedirectToPage(new { view = CurrentView, date = Input.StartDate.ToString("yyyy-MM-dd"), category = SelectedCategory, branchId = SelectedBranchId });
        }

        // ── Handler: Delete Event ──
        public async Task<IActionResult> OnPostDeleteEventAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var evt = await _context.CalendarEvents.FirstOrDefaultAsync(e => e.Id == id);
            if (evt == null)
            {
                TempData["ErrorMessage"] = "Event not found.";
                return RedirectToPage();
            }

            bool canDelete = (evt.CreatedByUserId == user.Id) || User.IsInRole("Admin") || User.IsInRole("HR Manager");
            if (!canDelete)
            {
                TempData["ErrorMessage"] = "You are not authorized to delete this event.";
                return RedirectToPage();
            }

            _context.CalendarEvents.Remove(evt);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Event deleted from calendar.";
            return RedirectToPage(new { view = CurrentView, date = CurrentDate.ToString("yyyy-MM-dd"), category = SelectedCategory, branchId = SelectedBranchId });
        }

        // ── View Models ──
        public class CalendarDisplayItem
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
            public string Category { get; set; } = "Event";
            public string CategoryColor { get; set; } = "#10823c";
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public bool IsAllDay { get; set; }
            public string Location { get; set; } = "";
            public string MeetingLink { get; set; } = "";
            public string? TrainerName { get; set; }
            public int AttendeeCount { get; set; }
            public List<string> Attendees { get; set; } = new();
            public bool IsTraining { get; set; }
            public bool CanEdit { get; set; }
            public bool CanDelete { get; set; }
            public string CreatorName { get; set; } = "";
            public string BranchName { get; set; } = "";
            public string DepartmentName { get; set; } = "";
        }

        public class CalendarDayCell
        {
            public DateTime Date { get; set; }
            public bool IsCurrentMonth { get; set; }
            public bool IsToday { get; set; }
            public int DayNumber { get; set; }
            public List<CalendarDisplayItem> Events { get; set; } = new();
        }

        public class EventInputModel
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
            public string? Description { get; set; }
            public string EventType { get; set; } = "Meeting"; // "Meeting", "Personal", "Reminder", "Event", "Holiday"
            public DateTime StartDate { get; set; } = SriLankaTime.Today;
            public TimeSpan StartTime { get; set; } = new TimeSpan(9, 0, 0);
            public TimeSpan EndTime { get; set; } = new TimeSpan(10, 0, 0);
            public bool IsAllDay { get; set; } = false;
            public string? Location { get; set; }
            public string? MeetingLink { get; set; }
            public int? BranchId { get; set; }
            public int? DepartmentId { get; set; }
        }
    }
}
