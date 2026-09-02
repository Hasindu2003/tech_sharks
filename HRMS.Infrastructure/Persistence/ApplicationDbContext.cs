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

// Death
using HRMS.Domain.Entities.Death;

// Welfare & Payroll
using HRMS.Domain.Entities.Welfare;
using HRMS.Domain.Entities.Payroll;

// Calendar
using HRMS.Domain.Entities.Calendar;

namespace HRMS.Infrastructure.Persistence
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // ---------------- Calendar ----------------
        public DbSet<CalendarEvent> CalendarEvents { get; set; } = null!;

        // ---------------- Core ----------------
        public DbSet<Branch> Branches { get; set; } = null!;
        public DbSet<Department> Departments { get; set; } = null!;
        public DbSet<BranchDepartment> BranchDepartments { get; set; } = null!;
        public DbSet<DepartmentDesignation> DepartmentDesignations { get; set; } = null!;
        public DbSet<Employee> Employees { get; set; } = null!;
        public DbSet<DraftEmployee> DraftEmployees { get; set; } = null!;
        public DbSet<Designation> Designations { get; set; } = null!;
        public DbSet<EmployeeDocument> EmployeeDocuments { get; set; } = null!;
        public DbSet<HRMS.Domain.Entities.Core.Notification> Notifications { get; set; } = null!;
        public DbSet<HRMS.Domain.Entities.Core.BugReport> BugReports { get; set; } = null!;

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

        // ---------------- Termination ----------------
        public DbSet<TerminationRequest> TerminationRequests { get; set; } = null!;
        public DbSet<TerminationDocument> TerminationDocuments { get; set; } = null!;
        public DbSet<TerminationDepartmentReview> TerminationDepartmentReviews { get; set; } = null!;

        // ---------------- Resignation ----------------
        public DbSet<ResignationRequest> ResignationRequests { get; set; } = null!;
        public DbSet<ResignationDocument> ResignationDocuments { get; set; } = null!;
        public DbSet<ResignationDepartmentReview> ResignationDepartmentReviews { get; set; } = null!;

        // ---------------- Death ----------------
        public DbSet<DeathRequest> DeathRequests { get; set; } = null!;
        public DbSet<DeathDocument> DeathDocuments { get; set; } = null!;

        // ---------------- Welfare ----------------
        public DbSet<WelfareRequest> WelfareRequests { get; set; } = null!;
        public DbSet<WelfareType> WelfareTypes { get; set; } = null!;
        public DbSet<WelfareApproval> WelfareApprovals { get; set; } = null!;
        public DbSet<WelfareDocument> WelfareDocuments { get; set; } = null!;

        // ---------------- Payroll ----------------
        public DbSet<PayrollSalary> PayrollSalaries { get; set; } = null!;
        public DbSet<PayrollRun> PayrollRuns { get; set; } = null!;
        public DbSet<Payslip> Payslips { get; set; } = null!;
        public DbSet<PayrollBonus> PayrollBonuses { get; set; } = null!;
        public DbSet<PayrollPolicySetting> PayrollPolicySettings { get; set; } = null!;

        // ---------------- Recruitment (CV Bank & Job Openings) ----------------
        public DbSet<HRMS.Domain.Entities.CVBank> CVBanks { get; set; } = null!;
        public DbSet<HRMS.Domain.Entities.Recruitment.JobOpening> JobOpenings { get; set; } = null!;

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

            builder.Entity<HRMS.Domain.Entities.Core.Notification>(entity =>
            {
                entity.HasIndex(e => new { e.UserId, e.IsRead });
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

            builder.Entity<DeathRequest>(entity =>
            {
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.InitiatedBy);
                entity.HasIndex(e => e.EpfNumber);
                entity.HasIndex(e => e.CreatedDate);
                entity.HasMany(e => e.Documents)
                      .WithOne(d => d.DeathRequest)
                      .HasForeignKey(d => d.DeathRequestId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<DeathDocument>(entity =>
            {
                entity.HasIndex(e => e.DeathRequestId);
            });

            // ── Welfare Request ───────────────────────────────────────────────
            builder.Entity<WelfareRequest>(entity =>
            {
                entity.HasKey(e => e.RequestId);
                entity.ToTable("welfarerequest");
                entity.Property(e => e.RequestId).HasColumnName("request_id");
                entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
                entity.Property(e => e.WelfareTypeId).HasColumnName("welfare_type_id");
                entity.Property(e => e.RequestDate).HasColumnName("request_date");
                entity.Property(e => e.RequestedAmount).HasColumnName("requested_amount");
                entity.Property(e => e.ApprovedAmount).HasColumnName("approved_amount");
                entity.Property(e => e.Remark).HasColumnName("remark");
                entity.Property(e => e.Status).HasColumnName("status");
                entity.Property(e => e.IsDraft).HasColumnName("is_draft");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.SubmittedBy).HasColumnName("submitted_by");
                entity.Property(e => e.CurrentLevel).HasColumnName("current_level");
                entity.Property(e => e.CurrentStatus).HasColumnName("current_status");

                entity.HasOne(e => e.Employee)
                      .WithMany()
                      .HasForeignKey(e => e.EmployeeId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.WelfareType)
                      .WithMany(t => t.WelfareRequests)
                      .HasForeignKey(e => e.WelfareTypeId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.Documents)
                      .WithOne(d => d.WelfareRequest)
                      .HasForeignKey(d => d.RequestId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── Welfare Type ──────────────────────────────────────────────────
            builder.Entity<WelfareType>(entity =>
            {
                entity.HasKey(e => e.WelfareTypeId);
                entity.ToTable("welfaretype");
                entity.Property(e => e.WelfareTypeId).HasColumnName("welfare_type_id");
                entity.Property(e => e.TypeName).HasColumnName("type_name");
                entity.Property(e => e.Category).HasColumnName("category");
                entity.Property(e => e.MaxEligibleAmount).HasColumnName("max_eligible_amount");
                entity.Property(e => e.IsActive).HasColumnName("is_active");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });

            // ── Welfare Approval ──────────────────────────────────────────────
            builder.Entity<WelfareApproval>(entity =>
            {
                entity.HasKey(e => e.ApprovalId);
                entity.ToTable("welfareapproval");
                entity.Property(e => e.ApprovalId).HasColumnName("approval_id");
                entity.Property(e => e.RequestId).HasColumnName("request_id");
                entity.Property(e => e.ApproverLevel).HasColumnName("approver_level");
                entity.Property(e => e.ApproverId).HasColumnName("approver_id");
                entity.Property(e => e.Action).HasColumnName("action");
                entity.Property(e => e.Comments).HasColumnName("comments");
                entity.Property(e => e.ActionDate).HasColumnName("action_date");

                entity.HasOne(e => e.WelfareRequest)
                      .WithMany()
                      .HasForeignKey(e => e.RequestId)
                      .OnDelete(DeleteBehavior.Cascade)
                      .HasConstraintName("FK_WelfareApproval_WelfareRequest");
            });

            // ── Welfare Document ──────────────────────────────────────────────
            builder.Entity<WelfareDocument>(entity =>
            {
                entity.HasKey(e => e.DocumentId);
                entity.ToTable("welfaredocument");
                entity.Property(e => e.DocumentId).HasColumnName("document_id");
                entity.Property(e => e.RequestId).HasColumnName("request_id");
                entity.Property(e => e.FileName).HasColumnName("file_name");
                entity.Property(e => e.FilePath).HasColumnName("file_path");
                entity.Property(e => e.FileType).HasColumnName("file_type");
                entity.Property(e => e.UploadedAt).HasColumnName("uploaded_at");

                entity.HasOne(e => e.WelfareRequest)
                      .WithMany(r => r.Documents)
                      .HasForeignKey(e => e.RequestId)
                      .OnDelete(DeleteBehavior.Cascade)
                      .HasConstraintName("FK_WelfareDocument_WelfareRequest");
            });

            // ── Payroll Salary ────────────────────────────────────────────────
            builder.Entity<PayrollSalary>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.ToTable("payrollsalary");
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.EmployeeId).HasColumnName("EmployeeId");
                entity.Property(e => e.BasicSalary).HasColumnName("BasicSalary");
                entity.Property(e => e.HousingAllowance).HasColumnName("HousingAllowance");
                entity.Property(e => e.TransportAllowance).HasColumnName("TransportAllowance");
                entity.Property(e => e.MedicalAllowance).HasColumnName("MedicalAllowance");
                entity.Property(e => e.EffectiveDate).HasColumnName("EffectiveDate");

                entity.HasOne(e => e.Employee)
                      .WithMany()
                      .HasForeignKey(e => e.EmployeeId)
                      .HasConstraintName("FK_PayrollSalary_Employee");
            });

            // ── Payroll Run ───────────────────────────────────────────────────
            builder.Entity<PayrollRun>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.ToTable("payrollrun");
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.Month).HasColumnName("Month");
                entity.Property(e => e.Year).HasColumnName("Year");
                entity.Property(e => e.BranchId).HasColumnName("BranchId");
                entity.Property(e => e.Status).HasColumnName("Status");
                entity.Property(e => e.ProcessedAt).HasColumnName("ProcessedAt");
                entity.Property(e => e.TotalAmount).HasColumnName("TotalAmount");
                entity.Property(e => e.TotalEmployees).HasColumnName("TotalEmployees");
                entity.Ignore(e => e.MonthName);

                entity.HasOne(e => e.Branch)
                      .WithMany()
                      .HasForeignKey(e => e.BranchId)
                      .HasConstraintName("FK_PayrollRun_Branch")
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ── Payslip ───────────────────────────────────────────────────────
            builder.Entity<Payslip>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.ToTable("payslip");
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.PayrollRunId).HasColumnName("PayrollRunId");
                entity.Property(e => e.EmployeeId).HasColumnName("EmployeeId");
                entity.Property(e => e.BasicSalary).HasColumnName("BasicSalary");
                entity.Property(e => e.HousingAllowance).HasColumnName("HousingAllowance");
                entity.Property(e => e.TransportAllowance).HasColumnName("TransportAllowance");
                entity.Property(e => e.MedicalAllowance).HasColumnName("MedicalAllowance");
                entity.Property(e => e.Bonuses).HasColumnName("Bonuses");
                entity.Property(e => e.GrossPay).HasColumnName("GrossPay");
                entity.Property(e => e.EpfEmployee).HasColumnName("EpfEmployee");
                entity.Property(e => e.EpfEmployer).HasColumnName("EpfEmployer");
                entity.Property(e => e.Etf).HasColumnName("Etf");
                entity.Property(e => e.TaxDeduction).HasColumnName("TaxDeduction");
                entity.Property(e => e.TotalDeductions).HasColumnName("TotalDeductions");
                entity.Property(e => e.NetPay).HasColumnName("NetPay");
                entity.Property(e => e.Status).HasColumnName("Status");

                entity.HasOne(e => e.PayrollRun)
                      .WithMany(r => r.Payslips)
                      .HasForeignKey(e => e.PayrollRunId)
                      .HasConstraintName("FK_Payslip_PayrollRun");

                entity.HasOne(e => e.Employee)
                      .WithMany()
                      .HasForeignKey(e => e.EmployeeId)
                      .HasConstraintName("FK_Payslip_Employee");
            });

            // ── Payroll Bonus ─────────────────────────────────────────────────
            builder.Entity<PayrollBonus>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.ToTable("payrollbonus");
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.EmployeeId).HasColumnName("EmployeeId");
                entity.Property(e => e.BonusType).HasColumnName("BonusType");
                entity.Property(e => e.Amount).HasColumnName("Amount");
                entity.Property(e => e.Month).HasColumnName("Month");
                entity.Property(e => e.Year).HasColumnName("Year");
                entity.Property(e => e.Reason).HasColumnName("Reason");

                entity.HasOne(e => e.Employee)
                      .WithMany()
                      .HasForeignKey(e => e.EmployeeId)
                      .HasConstraintName("FK_PayrollBonus_Employee");
            });

            // ── Training Module Mappings ──
            builder.Entity<TrainingProgramRequest>()
                .HasOne(t => t.Employee)
                .WithMany() 
                .HasForeignKey(t => t.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Training>()
                .Property(t => t.TrainerName)
                .HasColumnName("Trainer");

            // ── Employee Transfer (Multi-FK to Department/Designation) ──
            builder.Entity<EmployeeTransfer>(entity =>
            {
                entity.HasOne(e => e.FromDepartment)
                      .WithMany()
                      .HasForeignKey(e => e.FromDepartmentId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.ToDepartment)
                      .WithMany()
                      .HasForeignKey(e => e.ToDepartmentId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.FromDesignation)
                      .WithMany()
                      .HasForeignKey(e => e.FromDesignationId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.ToDesignation)
                      .WithMany()
                      .HasForeignKey(e => e.ToDesignationId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.Employee)
                      .WithMany()
                      .HasForeignKey(e => e.EmployeeId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // ── Transfer Approval ──
            builder.Entity<TransferApproval>(entity =>
            {
                entity.HasOne(e => e.EmployeeTransfer)
                      .WithMany(t => t.Approvals)
                      .HasForeignKey(e => e.EmployeeTransferId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // Disable cascade delete paths to avoid SQL Server cycles and multiple cascade path errors (Error 1785)
            foreach (var foreignKey in builder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                foreignKey.DeleteBehavior = DeleteBehavior.NoAction;
            }
        }
    }
}
