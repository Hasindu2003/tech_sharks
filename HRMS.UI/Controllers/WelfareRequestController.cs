using HRMS.Domain.Entities.Welfare;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WelfareRequestController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public WelfareRequestController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ── POST /api/WelfareRequest ──────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] WelfareRequestDto dto)
        {
            try
            {
                if (dto == null) return BadRequest(new { message = "Invalid data." });

                var request = new WelfareRequest
                {
                    EmployeeId = dto.EmployeeId,
                    WelfareTypeId = dto.WelfareTypeId,
                    RequestDate = DateTime.Parse(dto.RequestDate!),
                    RequestedAmount = dto.RequestedAmount,
                    Remark = dto.Remark,
                    IsDraft = dto.IsDraft,
                    Status = dto.IsDraft ? "Draft" : "Pending",
                    CurrentLevel = "BranchDGM",
                    CurrentStatus = "Pending",
                    CreatedAt = DateTime.Now,
                    SubmittedBy = dto.EmployeeId
                };

                _context.WelfareRequests.Add(request);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Success", requestId = request.RequestId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // ── GET /api/WelfareRequest/employee/{id} ─────────────────────────────
        [HttpGet("employee/{id}")]
        public async Task<IActionResult> GetEmployee(int id)
        {
            try
            {
                var employee = await _context.Employees
                    .Include(e => e.Designation)
                    .Include(e => e.Department)
                    .Include(e => e.Branch)
                    .FirstOrDefaultAsync(e => e.Id == id);

                if (employee == null)
                    return NotFound(new { message = "Employee not found." });

                return Ok(new
                {
                    id = employee.Id,
                    fullName = $"{employee.FirstName} {employee.MiddleName} {employee.LastName}".Trim(),
                    nic = employee.NIC,
                    email = employee.Email,
                    phoneNumber = employee.PhoneNumber,
                    epfNumber = employee.EPFNumber,
                    bankAccountName = employee.BankAccountName,
                    bankAccountNumber = employee.BankAccountNumber,
                    designation = employee.Designation?.Title,
                    department = employee.Department?.Name,
                    branch = employee.Branch?.Name,
                    dateJoined = employee.DateJoined.ToString("yyyy-MM-dd"),
                    status = employee.Status
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // ── POST /api/WelfareRequest/upload/{requestId} ───────────────────────
        [HttpPost("upload/{requestId}")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
        public async Task<IActionResult> UploadDocuments(int requestId, [FromForm] List<IFormFile> files)
        {
            try
            {
                if (files == null || files.Count == 0)
                    return BadRequest(new { message = "No files received." });

                var request = await _context.WelfareRequests
                    .FirstOrDefaultAsync(r => r.RequestId == requestId);

                if (request == null)
                    return NotFound(new { message = "Request not found." });

                // ── Build absolute save path using ContentRootPath (always works) ──
                var saveDir = Path.Combine(
                    _env.ContentRootPath,   // e.g. C:\...\HRMS.UI
                    "wwwroot",
                    "uploads",
                    "welfare",
                    requestId.ToString()
                );

                // Ensure the full path exists — creates all missing folders at once
                Directory.CreateDirectory(saveDir);

                var uploadedCount = 0;

                foreach (var file in files)
                {
                    if (file.Length <= 0) continue;

                    var ext = Path.GetExtension(file.FileName);
                    var uniqueName = $"{Guid.NewGuid()}{ext}";
                    var fullPath = Path.Combine(saveDir, uniqueName);

                    // Save file to disk
                    await using (var stream = System.IO.File.Create(fullPath))
                    {
                        await file.CopyToAsync(stream);
                    }

                    // Save record to database
                    _context.WelfareDocuments.Add(new WelfareDocument
                    {
                        RequestId = requestId,
                        FileName = file.FileName,
                        FilePath = $"/uploads/welfare/{requestId}/{uniqueName}",
                        FileType = file.ContentType,
                        UploadedAt = DateTime.Now
                    });

                    uploadedCount++;
                }

                await _context.SaveChangesAsync();
                return Ok(new { message = $"{uploadedCount} file(s) uploaded successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
            }
        }
    }

    public class WelfareRequestDto
    {
        public int EmployeeId { get; set; }
        public int WelfareTypeId { get; set; }
        public string? RequestDate { get; set; }
        public decimal RequestedAmount { get; set; }
        public string? Remark { get; set; }
        public bool IsDraft { get; set; }
    }
}
