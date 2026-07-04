using HRMS.Application.Leave;
using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Leave
{
    [Authorize]
    public class ApplyModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILeaveService _leaveService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ApplyModel(
            ILeaveService leaveService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _leaveService = leaveService;
            _userManager = userManager;
            _context = context;
            _environment = environment;
        }

        public bool HasEmployeeProfile { get; set; }
        public List<LeavePolicy> Policies { get; set; } = new();

        [BindProperty] public LeaveType LeaveType { get; set; }

        [BindProperty] public DateTime StartDate { get; set; } = DateTime.Today;

        [BindProperty] public DateTime EndDate { get; set; } = DateTime.Today;

        [BindProperty] public bool IsHalfDay { get; set; }

        [BindProperty] public string? Reason { get; set; }

        [BindProperty] public IFormFile? Attachment { get; set; }

        [BindProperty] public DateTime? ExpectedDeliveryDate { get; set; }

        [BindProperty] public string? PassportNumber { get; set; }

        [BindProperty] public DateTime? PassportExpiry { get; set; }

        [BindProperty] public string? Country { get; set; }

        [BindProperty] public string? Purpose { get; set; }

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.EmployeeId == null)
                return Forbid();

            string? attachmentPath = null;
            if (Attachment is { Length: > 0 })
            {
                var uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads", "leave-attachments");
                Directory.CreateDirectory(uploadsRoot);

                var safeFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(Attachment.FileName)}";
                var fullPath = Path.Combine(uploadsRoot, safeFileName);

                await using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await Attachment.CopyToAsync(stream);
                }

                attachmentPath = $"/uploads/leave-attachments/{safeFileName}";
            }

            var request = new ApplyLeaveRequest
            {
                EmployeeId = user.EmployeeId.Value,
                LeaveType = LeaveType,
                StartDate = StartDate,
                EndDate = IsHalfDay ? StartDate : EndDate,
                IsHalfDay = IsHalfDay,
                Reason = Reason,
                AttachmentPath = attachmentPath,
                ExpectedDeliveryDate = ExpectedDeliveryDate,
                PassportNumber = PassportNumber,
                PassportExpiry = PassportExpiry,
                Country = Country,
                Purpose = Purpose
            };

            var result = await _leaveService.ApplyAsync(request);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                await LoadAsync();
                return Page();
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToPage("./Index");
        }

        private async Task LoadAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            HasEmployeeProfile = user?.EmployeeId != null;
            Policies = await _context.LeavePolicies.Where(p => p.Active).OrderBy(p => p.LeaveType).ToListAsync();
        }
    }
}
