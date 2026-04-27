using HRMS.Domain.Entities.Welfare;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WelfareApprovalController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public WelfareApprovalController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("branchdgm")]
        public async Task<IActionResult> BranchDGMAction([FromBody] ApprovalDto dto)
        {
            try
            {
                var request = await _context.WelfareRequests
                    .FirstOrDefaultAsync(r => r.RequestId == dto.RequestId);

                if (request == null)
                    return NotFound(new { message = "Request not found." });

                // Save approval record
                var approval = new WelfareApproval
                {
                    RequestId = dto.RequestId,
                    ApproverLevel = "BranchDGM",
                    ApproverId = dto.ApproverId,
                    Action = dto.Action,
                    Comments = dto.Comments,
                    ActionDate = DateTime.Now
                };
                _context.WelfareApprovals.Add(approval);

                // Update request status
                if (dto.Action == "Approved")
                {
                    request.CurrentLevel = "HODGM";
                    request.CurrentStatus = "Pending";
                }
                else if (dto.Action == "Rejected")
                {
                    request.CurrentLevel = "BranchDGM";
                    request.CurrentStatus = "Rejected";
                    request.Status = "Rejected";
                }
                else if (dto.Action == "SentBack")
                {
                    request.CurrentLevel = "BranchDGM";
                    request.CurrentStatus = "SentBack";
                }

                await _context.SaveChangesAsync();
                return Ok(new { message = "Success" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
            }
        }
    }

    public class ApprovalDto
    {
        public int RequestId { get; set; }
        public int ApproverId { get; set; }
        public string Action { get; set; } = null!;
        public string? Comments { get; set; }
    }
}