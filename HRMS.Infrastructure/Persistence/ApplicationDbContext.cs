using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

// Identity
using HRMS.Infrastructure.Identity;

// Core
using HRMS.Domain.Entities.Core;

// Attendance
using HRMS.Domain.Entities.Attendance;

// Leave
using HRMS.Domain.Entities.Leave;

// Training
using HRMS.Domain.Entities.Training;

// Transfer
using HRMS.Domain.Entities.Transfer;

// Termination
using HRMS.Domain.Entities.Termination;

// Resignation
using HRMS.Domain.Entities.Resignation;

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
        public DbSet<MaternityLeave> MaternityLeaves { get; set; } = null!;
        public DbSet<MaternityPayment> MaternityPayments { get; set; } = null!;
        public DbSet<OverseasLeave> OverseasLeaves { get; set; } = null!;

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
        public DbSet<TransferRequest> TransferRequests { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;

        // ---------------- Termination ----------------
        public DbSet<TerminationRequest> TerminationRequests { get; set; } = null!;
        public DbSet<TerminationDocument> TerminationDocuments { get; set; } = null!;

        // ---------------- Resignation ----------------
        public DbSet<ResignationRequest> ResignationRequests { get; set; } = null!;
        public DbSet<ResignationDocument> ResignationDocuments { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>(entity =>
            {
                entity.Ignore(e => e.PhoneNumber);
                entity.Ignore(e => e.PhoneNumberConfirmed);
                entity.Ignore(e => e.TwoFactorEnabled);
            });

            builder.Entity<IdentityUserLogin<string>>()
                .ToTable("AspNetUserLogins", t => t.ExcludeFromMigrations());

            builder.Entity<TransferRequest>(entity =>
            {
                entity.HasIndex(e => e.RequestedBy);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.RequestedDate);
                entity.HasIndex(e => e.EpfNumber);
            });

            builder.Entity<Notification>(entity =>
            {
                entity.HasIndex(e => new { e.RecipientEmail, e.IsRead });
                entity.HasIndex(e => e.CreatedAt);
            });

            builder.Entity<TerminationRequest>(entity =>
            {
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.InitiatedBy);
                entity.HasIndex(e => e.EpfNumber);
                entity.HasIndex(e => e.CreatedDate);
                entity.HasMany(e => e.Documents)
                      .WithOne(d => d.TerminationRequest)
                      .HasForeignKey(d => d.TerminationRequestId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<TerminationDocument>(entity =>
            {
                entity.HasIndex(e => e.TerminationRequestId);
            });

            builder.Entity<ResignationRequest>(entity =>
            {
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.InitiatedBy);
                entity.HasIndex(e => e.EpfNumber);
                entity.HasIndex(e => e.CreatedDate);
                entity.HasMany(e => e.Documents)
                      .WithOne(d => d.ResignationRequest)
                      .HasForeignKey(d => d.ResignationRequestId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ResignationDocument>(entity =>
            {
                entity.HasIndex(e => e.ResignationRequestId);
            });
        }
    }
}
