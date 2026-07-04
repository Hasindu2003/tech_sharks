using HRMS.Application.Attendance;
using HRMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.UI.Controllers.Api
{
    [Authorize]
    [ApiController]
    [Route("api/attendance")]
    public class AttendanceController : ControllerBase
    {
        private readonly IAttendanceService _attendanceService;
        private readonly UserManager<ApplicationUser> _userManager;

        public AttendanceController(IAttendanceService attendanceService, UserManager<ApplicationUser> userManager)
        {
            _attendanceService = attendanceService;
            _userManager = userManager;
        }

        [HttpPost("punch-in")]
        public async Task<IActionResult> PunchIn()
        {
            var employeeId = await ResolveEmployeeIdAsync();
            if (employeeId == null)
                return BadRequest(new { message = "No employee profile is linked to this account." });

            var result = await _attendanceService.PunchInAsync(employeeId.Value, DateTime.Now, "Manual");
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("punch-out")]
        public async Task<IActionResult> PunchOut()
        {
            var employeeId = await ResolveEmployeeIdAsync();
            if (employeeId == null)
                return BadRequest(new { message = "No employee profile is linked to this account." });

            var result = await _attendanceService.PunchOutAsync(employeeId.Value, DateTime.Now, "Manual");
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("today")]
        public async Task<IActionResult> Today()
        {
            var employeeId = await ResolveEmployeeIdAsync();
            if (employeeId == null)
                return BadRequest(new { message = "No employee profile is linked to this account." });

            var today = await _attendanceService.GetTodayAsync(employeeId.Value);
            return Ok(today);
        }

        [HttpGet("history")]
        public async Task<IActionResult> History([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var employeeId = await ResolveEmployeeIdAsync();
            if (employeeId == null)
                return BadRequest(new { message = "No employee profile is linked to this account." });

            var toDate = to ?? DateTime.Today;
            var fromDate = from ?? toDate.AddDays(-6);

            var history = await _attendanceService.GetHistoryAsync(employeeId.Value, fromDate, toDate);
            return Ok(history);
        }

        [HttpGet("month-summary")]
        public async Task<IActionResult> MonthSummary([FromQuery] int? year, [FromQuery] int? month)
        {
            var employeeId = await ResolveEmployeeIdAsync();
            if (employeeId == null)
                return BadRequest(new { message = "No employee profile is linked to this account." });

            var now = DateTime.Today;
            var summary =
                await _attendanceService.GetMonthSummaryAsync(employeeId.Value, year ?? now.Year, month ?? now.Month);
            return Ok(summary);
        }

        private async Task<int?> ResolveEmployeeIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.EmployeeId;
        }
    }
}
