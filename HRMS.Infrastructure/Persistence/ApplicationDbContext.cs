using HRMS.Domain.Entities.Attendance;
using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Leave;
using HRMS.Domain.Entities.Notifications;
using HRMS.Domain.Entities.Training;
using HRMS.Domain.Entities.Transfer;
using HRMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
// Identity

// Core

// Attendance

// Leave

// Notifications

// Training

// Transfer


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
        public DbSet<LeavePolicy> LeavePolicies { get; set; } = null!;
        public DbSet<Holiday> Holidays { get; set; } = null!;
        public DbSet<LeaveBalanceAdjustment> LeaveBalanceAdjustments { get; set; } = null!;
        public DbSet<MaternityLeave> MaternityLeaves { get; set; } = null!;
        public DbSet<MaternityPayment> MaternityPayments { get; set; } = null!;
        public DbSet<OverseasLeave> OverseasLeaves { get; set; } = null!;

        // ---------------- Notifications ----------------
        public DbSet<Notification> Notifications { get; set; } = null!;

        // ---------------- Training ----------------
        public DbSet<Training> Trainings { get; set; } = null!;
        public DbSet<EmployeeTraining> EmployeeTrainings { get; set; } = null!;
        public DbSet<Trainer> Trainers { get; set; } = null!;
        public DbSet<TrainingProgramRequest> TrainingProgramRequests { get; set; } = null!;
        public DbSet<TrainingFeedback> TrainingFeedbacks { get; set; } = null!;
        public DbSet<InternProgram> InternPrograms { get; set; } = null!;
        public DbSet<InternFeedback> InternFeedbacks { get; set; } = null!;
        public DbSet<ProbationPeriod> ProbationPeriods { get; set; } = null!;
        public DbSet<ProbationFeedback> ProbationFeedbacks { get; set; } = null!;

        // ---------------- Transfer ----------------
        public DbSet<EmployeeTransfer> EmployeeTransfers { get; set; } = null!;
        public DbSet<TransferApproval> TransferApprovals { get; set; } = null!;


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Remove unused Identity columns from AspNetUsers
            builder.Entity<ApplicationUser>(entity =>
            {
                entity.Ignore(e => e.PhoneNumber);
                entity.Ignore(e => e.PhoneNumberConfirmed);
                entity.Ignore(e => e.TwoFactorEnabled);
            });

            // Remove unused Identity tables
            builder.Entity<IdentityUserLogin<string>>()
                .ToTable("AspNetUserLogins", t => t.ExcludeFromMigrations());

            // Self-referencing manager FK — Restrict avoids a cascade path back onto Employees.
            builder.Entity<Employee>()
                .HasOne(e => e.Manager)
                .WithMany()
                .HasForeignKey(e => e.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            // LeaveApproval already cascades from Leave->Employee; the direct actor FK would create
            // a second cascade path onto Employees, so it must be Restrict.
            builder.Entity<LeaveApproval>()
                .HasOne(a => a.ActorEmployee)
                .WithMany()
                .HasForeignKey(a => a.ActorEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<LeaveEntitlement>(entity =>
            {
                entity.HasOne(e => e.Employee)
                    .WithMany()
                    .HasForeignKey(e => e.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.EmployeeId, e.LeaveType, e.Year }).IsUnique();
            });

            builder.Entity<LeaveBalanceAdjustment>()
                .HasOne(a => a.AdjustedByEmployee)
                .WithMany()
                .HasForeignKey(a => a.AdjustedByEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Notification>()
                .HasOne(n => n.Employee)
                .WithMany()
                .HasForeignKey(n => n.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Seed the six leave policies from the business requirements — HR can tune them afterwards.
            builder.Entity<LeavePolicy>().HasData(
                new LeavePolicy
                {
                    Id = 1, LeaveType = LeaveType.Annual, Name = "Annual Leave", DaysPerYear = 14,
                    IsPaid = true, AffectsBalance = true, RequiresAttachment = false, AllowHalfDay = true,
                    ExcludeWeekends = true, ExcludeHolidays = true, AllowPastDates = false,
                    CarryForwardAllowed = true, MaxCarryForwardDays = 7, Active = true
                },
                new LeavePolicy
                {
                    Id = 2, LeaveType = LeaveType.Casual, Name = "Casual Leave", DaysPerYear = 7,
                    IsPaid = true, AffectsBalance = true, RequiresAttachment = false, AllowHalfDay = true,
                    ExcludeWeekends = true, ExcludeHolidays = true, AllowPastDates = false,
                    CarryForwardAllowed = false, MaxCarryForwardDays = null, Active = true
                },
                new LeavePolicy
                {
                    Id = 3, LeaveType = LeaveType.Sick, Name = "Sick Leave", DaysPerYear = 14,
                    IsPaid = true, AffectsBalance = true, RequiresAttachment = true, AllowHalfDay = true,
                    ExcludeWeekends = true, ExcludeHolidays = true, AllowPastDates = true,
                    CarryForwardAllowed = false, MaxCarryForwardDays = null, Active = true
                },
                new LeavePolicy
                {
                    Id = 4, LeaveType = LeaveType.Maternity, Name = "Maternity Leave", DaysPerYear = 84,
                    IsPaid = true, AffectsBalance = true, RequiresAttachment = true, AllowHalfDay = false,
                    ExcludeWeekends = false, ExcludeHolidays = false, AllowPastDates = true,
                    CarryForwardAllowed = false, MaxCarryForwardDays = null, Active = true
                },
                new LeavePolicy
                {
                    Id = 5, LeaveType = LeaveType.Overseas, Name = "Overseas Leave", DaysPerYear = null,
                    IsPaid = true, AffectsBalance = false, RequiresAttachment = false, AllowHalfDay = false,
                    ExcludeWeekends = true, ExcludeHolidays = true, AllowPastDates = false,
                    CarryForwardAllowed = false, MaxCarryForwardDays = null, Active = true
                },
                new LeavePolicy
                {
                    Id = 6, LeaveType = LeaveType.NoPay, Name = "No Pay Leave", DaysPerYear = null,
                    IsPaid = false, AffectsBalance = false, RequiresAttachment = false, AllowHalfDay = true,
                    ExcludeWeekends = true, ExcludeHolidays = true, AllowPastDates = false,
                    CarryForwardAllowed = false, MaxCarryForwardDays = null, Active = true
                }
            );
        }
    }
}
