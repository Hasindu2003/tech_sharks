using HRMS.Application.Payroll;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.UI.Controllers.Api
{
    // Read-only export consumed by a future Payroll module.
    [Authorize(Roles = "Admin,HR Manager")]
    [ApiController]
    [Route("api/payroll")]
    public class PayrollController : ControllerBase
    {
        private readonly IPayrollLeaveExportService _payrollLeaveExportService;

        public PayrollController(IPayrollLeaveExportService payrollLeaveExportService)
        {
            _payrollLeaveExportService = payrollLeaveExportService;
        }

        [HttpGet("leave-summary")]
        public async Task<IActionResult> LeaveSummary(
            [FromQuery] int employeeId, [FromQuery] DateTime from, [FromQuery] DateTime to)
        {
            var summary = await _payrollLeaveExportService.GetPayrollLeaveSummaryAsync(employeeId, from, to);
            return Ok(summary);
        }
    }
}
