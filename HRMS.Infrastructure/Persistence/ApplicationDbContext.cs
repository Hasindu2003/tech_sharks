using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Identity;
using HRMS.Domain.Entities.Recruitment;
using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Attendance;
using HRMS.Domain.Entities.Leave;
using HRMS.Domain.Entities.Training;
using HRMS.Domain.Entities.Transfer;

namespace HRMS.Infrastructure.Persistence
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // ---------------- Core ----------------
        public DbSet<Branch> Branches { get; set; } = null!;
        public DbSet<Department> Departments { get; set; } = null!;
        public DbSet<Employee> Employees { get; set; } = null!;
        public DbSet<Designation> Designations { get; set; } = null!;

        // ---------------- Attendance ----------------
        public DbSet<Attendance> Attendances { get; set; } = null!;
        public DbSet<BiometricLog> BiometricLogs { get; set; } = null!;
        public DbSet<AttendanceCorrection> AttendanceCorrections { get; set; } = null!;

        // ---------------- Leave ----------------
        public DbSet<Leave> Leaves { get; set; } = null!;
        public DbSet<LeaveEntitlement> LeaveEntitlements { get; set; } = null!;
        public DbSet<LeaveApproval> LeaveApprovals { get; set; } = null!;

        // ---------------- Training ----------------
        public DbSet<Training> Trainings { get; set; } = null!;
        public DbSet<EmployeeTraining> EmployeeTrainings { get; set; } = null!;
        public DbSet<TrainingProgramRequest> TrainingProgramRequests { get; set; } = null!;
        public DbSet<Trainer> Trainers { get; set; } = null!;
        public DbSet<TrainingFeedback> TrainingFeedbacks { get; set; } = null!;
        public DbSet<InternProgram> InternPrograms { get; set; } = null!;
        public DbSet<InternFeedback> InternFeedbacks { get; set; } = null!;
        public DbSet<ProbationPeriod> ProbationPeriods { get; set; } = null!;
        public DbSet<ProbationFeedback> ProbationFeedbacks { get; set; } = null!;

        // ---------------- Transfer ----------------
        public DbSet<EmployeeTransfer> EmployeeTransfers { get; set; } = null!;
        public DbSet<TransferApproval> TransferApprovals { get; set; } = null!;

        // ---------------- Requirement ----------------
        public DbSet<Candidate> Candidates { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TrainingProgramRequest>()
                .HasOne(t => t.Employee)
                .WithMany() 
                .HasForeignKey(t => t.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}