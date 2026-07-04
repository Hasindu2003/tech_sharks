using HRMS.Application.Attendance;
using HRMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Attendance
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly IAttendanceService _attendanceService;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(IAttendanceService attendanceService, UserManager<ApplicationUser> userManager)
        {
            _attendanceService = attendanceService;
            _userManager = userManager;
        }

        public bool HasEmployeeProfile { get; set; }
        public TodayAttendanceDto? Today { get; set; }
        public List<AttendanceHistoryItemDto> RecentHistory { get; set; } = new();

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.EmployeeId == null)
            {
                HasEmployeeProfile = false;
                return;
            }

            HasEmployeeProfile = true;
            Today = await _attendanceService.GetTodayAsync(user.EmployeeId.Value);
            RecentHistory = await _attendanceService.GetHistoryAsync(
                user.EmployeeId.Value, DateTime.Today.AddDays(-6), DateTime.Today);
        }
    }
}
