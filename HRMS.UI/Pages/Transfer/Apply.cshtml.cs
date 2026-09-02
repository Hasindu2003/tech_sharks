using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.Transfer
{
    [Authorize]
    public class ApplyModel : PageModel
    {
        private readonly ITransferRequestService _transferService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ApplyModel(ITransferRequestService transferService, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _transferService = transferService;
            _userManager = userManager;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public List<string> AvailableBranches { get; set; } = new();

        public class InputModel
        {
            public string EmployeeName { get; set; } = string.Empty;
            public string EpfNumber { get; set; } = string.Empty;
            public string CurrentBranch { get; set; } = string.Empty;
            public string CurrentDesignation { get; set; } = string.Empty;
            public string Department { get; set; } = string.Empty;

            [Required(ErrorMessage = "Please select the branch you want to transfer to.")]
            [Display(Name = "Requested Branch")]
            public string RequestedBranch { get; set; } = string.Empty;

            [Required(ErrorMessage = "Preferred transfer date is required.")]
            [DataType(DataType.Date)]
            [Display(Name = "Preferred Transfer Date")]
            public DateTime? PreferredDate { get; set; }

            [Required(ErrorMessage = "Reason for transfer is required.")]
            [StringLength(500, MinimumLength = 20,
                ErrorMessage = "Reason must be between 20 and 500 characters.")]
            [Display(Name = "Reason for Transfer")]
            public string Reason { get; set; } = string.Empty;

            [Display(Name = "Supporting Document")]
            public IFormFile? Document { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            await PopulateUserDetailsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await PopulateUserDetailsAsync();

            if (Input.PreferredDate.HasValue)
            {
                var minDate = DateTime.Today.AddDays(7);
                var maxDate = DateTime.Today.AddYears(1);

                if (Input.PreferredDate.Value.Date < minDate)
                {
                    ModelState.AddModelError("Input.PreferredDate",
                        "Preferred date must be at least 7 days from today.");
                }
                else if (Input.PreferredDate.Value.Date > maxDate)
                {
                    ModelState.AddModelError("Input.PreferredDate",
                        "Preferred date cannot be more than 1 year from today.");
                }
            }

            if (Input.RequestedBranch == Input.CurrentBranch)
            {
                ModelState.AddModelError("Input.RequestedBranch",
                    "You cannot request a transfer to your current branch.");
            }

            if (Input.Document != null && Input.Document.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError("Input.Document", "File size must not exceed 5 MB.");
            }

            if (Input.Document != null)
            {
                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };
                var extension = Path.GetExtension(Input.Document.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("Input.Document",
                        "Only PDF, DOC, DOCX, JPG, and PNG files are allowed.");
                }
            }

            if (!ModelState.IsValid)
                return Page();

            var user = await ResolveCurrentUserAsync();
            if (user == null) return Challenge();

            var employee = await ResolveEmployeeAsync(user);

            var joinDate = employee?.DateJoined ?? (user.DateOfJoining != default ? user.DateOfJoining : (DateTime?)null);
            var yearsOfService = joinDate.HasValue ? Math.Max(0, (int)((DateTime.Today - joinDate.Value).TotalDays / 365.25)) : 0;

            var userRole = User.IsInRole("HR Manager") ? "HR Manager"
                         : User.IsInRole("Area Manager") ? "Area Manager"
                         : User.IsInRole("Branch Manager") ? "Branch Manager"
                         : User.IsInRole("Department Head") ? "Department Head"
                         : User.IsInRole("Welfare Manager") ? "Welfare Manager"
                         : User.IsInRole("Admin") ? "Admin"
                         : "Employee";

            byte[]? documentData = null;
            string? documentFileName = null;
            string? documentContentType = null;

            if (Input.Document != null)
            {
                using var memoryStream = new MemoryStream();
                await Input.Document.CopyToAsync(memoryStream);
                documentData = memoryStream.ToArray();
                documentFileName = Input.Document.FileName;
                documentContentType = Input.Document.ContentType;
            }

            var request = new TransferRequestViewModel
            {
                EmployeeName = FirstNonBlank(employee?.FullName, employee?.Initials, user.FullName),
                EpfNumber = FirstNonBlank(employee?.EPFNumber, user.EpfNumber),
                EmployeeEmail = FirstNonBlank(employee?.Email, user.Email, user.UserName),
                CurrentBranch = FirstNonBlank(employee?.Branch?.Name, user.Branch),
                CurrentDesignation = await ResolveDesignationAsync(employee, user),
                Department = FirstNonBlank(employee?.Department?.Name, user.Department),
                RequestedBranch = Input.RequestedBranch,
                Reason = Input.Reason,
                PreferredDate = Input.PreferredDate!.Value,
                YearsOfService = yearsOfService,
                JoinDate = joinDate,
                RequestedBy = user.Email ?? user.UserName ?? "",
                RequestedByRole = userRole
            };

            await _transferService.CreateTransferRequestAsync(request, documentData, documentFileName, documentContentType);

            TempData["SuccessMessage"] = "Transfer request submitted successfully!";
            return RedirectToPage("/Transfer/MyRequests");
        }

        private async Task PopulateUserDetailsAsync()
        {
            var user = await ResolveCurrentUserAsync();
            if (user == null) return;

            var employee = await ResolveEmployeeAsync(user);

            Input.EmployeeName = FirstNonBlank(employee?.FullName, employee?.Initials, user.FullName);
            Input.EpfNumber = FirstNonBlank(employee?.EPFNumber, user.EpfNumber);
            Input.CurrentBranch = FirstNonBlank(employee?.Branch?.Name, user.Branch);
            Input.CurrentDesignation = await ResolveDesignationAsync(employee, user);
            Input.Department = FirstNonBlank(employee?.Department?.Name, user.Department);

            var dbBranches = await _context.Branches
                .OrderBy(b => b.Name)
                .Select(b => b.Name)
                .ToListAsync();

            AvailableBranches = dbBranches
                .Where(b => !string.Equals(b, Input.CurrentBranch, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private async Task<ApplicationUser?> ResolveCurrentUserAsync()
        {
            var username = User.Identity?.Name;
            var user = await _userManager.GetUserAsync(User);
            if (user == null && !string.IsNullOrEmpty(username))
            {
                user = await _userManager.FindByNameAsync(username) ?? await _userManager.FindByEmailAsync(username);
            }
            return user;
        }

        private async Task<HRMS.Domain.Entities.Core.Employee?> ResolveEmployeeAsync(ApplicationUser user)
        {
            var query = _context.Employees
                .Include(e => e.Branch)
                .Include(e => e.Department)
                .Include(e => e.Designation);

            if (user.EmployeeId.HasValue)
            {
                var byId = await query.FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value);
                if (byId != null) return byId;
            }

            foreach (var email in new[] { user.Email, user.UserName, User.Identity?.Name })
            {
                if (string.IsNullOrEmpty(email)) continue;
                var match = await query.FirstOrDefaultAsync(e => e.Email == email);
                if (match != null) return match;
            }

            return null;
        }

        /// <summary>
        /// Designation can be missing from the loaded navigation property while the foreign key
        /// is still set, and the linked login account may hold a blank string rather than null,
        /// so fall back through both before giving up.
        /// </summary>
        private async Task<string> ResolveDesignationAsync(
            HRMS.Domain.Entities.Core.Employee? employee, ApplicationUser user)
        {
            var title = FirstNonBlank(employee?.Designation?.Title, user.Designation);
            if (!string.IsNullOrWhiteSpace(title)) return title;

            if (employee?.DesignationId is int designationId && designationId > 0)
            {
                title = await _context.Designations
                    .Where(d => d.Id == designationId)
                    .Select(d => d.Title)
                    .FirstOrDefaultAsync() ?? string.Empty;
            }

            return title;
        }

        private static string FirstNonBlank(params string?[] values)
            => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;
    }
}