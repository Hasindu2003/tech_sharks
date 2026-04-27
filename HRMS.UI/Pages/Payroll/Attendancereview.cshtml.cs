using HRMS.Infrastructure.Persistence;
using HRMS.UI.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Payroll
{
    [Authorize(Roles = "Admin,Finance,SeniorManagement")]
    public class AttendanceReviewModel : BasePageModel
    {
        public AttendanceReviewModel(ApplicationDbContext db) : base(db) { }

        public List<AttendanceRecord> AttendanceData { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadCurrentUserAsync();

            AttendanceData = new List<AttendanceRecord>
            {
                new() { Name="Kamal Perera",       EmpId="EMP-001", Department="Engineering",     WorkingDays=20, TotalDays=22, PaidLeaves=2, NoPayLeaves=0, OvertimeHours=12, Status="Verified" },
                new() { Name="Nimasha Fernando",   EmpId="EMP-002", Department="Human Resources", WorkingDays=17, TotalDays=22, PaidLeaves=1, NoPayLeaves=4, OvertimeHours=0,  Status="Anomaly"  },
                new() { Name="Ruwan Silva",         EmpId="EMP-003", Department="Finance",         WorkingDays=21, TotalDays=22, PaidLeaves=1, NoPayLeaves=0, OvertimeHours=5,  Status="Pending"  },
                new() { Name="Dilani Jayawardena", EmpId="EMP-004", Department="Marketing",       WorkingDays=18, TotalDays=22, PaidLeaves=0, NoPayLeaves=4, OvertimeHours=0,  Status="Verified" },
                new() { Name="Chamara Bandara",    EmpId="EMP-005", Department="Operations",      WorkingDays=22, TotalDays=22, PaidLeaves=0, NoPayLeaves=0, OvertimeHours=8,  Status="Verified" },
                new() { Name="Tharaka Rajapaksa",  EmpId="EMP-006", Department="Engineering",     WorkingDays=19, TotalDays=22, PaidLeaves=3, NoPayLeaves=0, OvertimeHours=0,  Status="Verified" },
                new() { Name="Sanduni W.",          EmpId="EMP-007", Department="Human Resources", WorkingDays=16, TotalDays=22, PaidLeaves=2, NoPayLeaves=4, OvertimeHours=0,  Status="Anomaly"  },
                new() { Name="Lasith Kumara",      EmpId="EMP-008", Department="Finance",         WorkingDays=22, TotalDays=22, PaidLeaves=0, NoPayLeaves=0, OvertimeHours=15, Status="Verified" },
                new() { Name="Priyanka D.",         EmpId="EMP-009", Department="Marketing",       WorkingDays=20, TotalDays=22, PaidLeaves=2, NoPayLeaves=0, OvertimeHours=3,  Status="Pending"  },
                new() { Name="Mahesh Gunawardena", EmpId="EMP-010", Department="Operations",      WorkingDays=21, TotalDays=22, PaidLeaves=1, NoPayLeaves=0, OvertimeHours=6,  Status="Verified" },
            };
        }
    }

    public class AttendanceRecord
    {
        public string Name { get; set; } = string.Empty;
        public string EmpId { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public int WorkingDays { get; set; }
        public int TotalDays { get; set; }
        public int PaidLeaves { get; set; }
        public int NoPayLeaves { get; set; }
        public int OvertimeHours { get; set; }
        public string Status { get; set; } = "Pending";
    }
}
