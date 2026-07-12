using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;

namespace HRMS.UI.Pages.Employees
{
    using Employee = HRMS.Domain.Entities.Core.Employee;

    [Authorize(Roles = "HR Manager")]

    public class ReviewDocumentModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewDocumentModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public EmployeeDocument? Document { get; set; }
        public Employee? Employee { get; set; }

        [BindProperty]
        public string? RejectionNotes { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Document = await _context.EmployeeDocuments
                .Include(d => d.Employee)
                    .ThenInclude(e => e.Department)
                .Include(d => d.Employee)
                    .ThenInclude(e => e.Designation)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (Document == null)
                return NotFound();

            Employee = Document.Employee;
            return Page();
        }

        // Approve
        public async Task<IActionResult> OnPostApproveAsync(int id)
        {
            var doc = await _context.EmployeeDocuments
                .Include(d => d.Employee)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doc != null)
            {
                doc.Status           = "Approved";
                doc.ReviewedAt       = DateTime.Now;
                doc.ReviewedByUserId = _userManager.GetUserId(User);
                
                _context.Update(doc); // Force tracking update

                var empUser = await _userManager.FindByEmailAsync(doc.Employee.Email);
                if (empUser != null)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = empUser.Id,
                        Title = "Document Approved",
                        Message = $"Your document '{doc.FileName}' has been approved.",
                        TargetUrl = "/Profile?tab=documents",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
            }
            return RedirectToPage("/Employees/Index", new { tab = "documents" });
        }

        // Reject
        public async Task<IActionResult> OnPostRejectAsync(int id)
        {
            var doc = await _context.EmployeeDocuments
                .Include(d => d.Employee)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doc != null)
            {
                doc.Status           = "Rejected";
                doc.ReviewedAt       = DateTime.Now;
                doc.ReviewedByUserId = _userManager.GetUserId(User);
                doc.ReviewerNotes    = RejectionNotes;

                _context.Update(doc); // Force tracking update

                var empUser = await _userManager.FindByEmailAsync(doc.Employee.Email);
                if (empUser != null)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = empUser.Id,
                        Title = "Document Rejected",
                        Message = $"Your document '{doc.FileName}' was rejected. {(string.IsNullOrEmpty(RejectionNotes) ? "" : $"Reason: {RejectionNotes}")}",
                        TargetUrl = "/Profile?tab=documents",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
            }
            return RedirectToPage("/Employees/Index", new { tab = "documents" });
        }
    }
}
