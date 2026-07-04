using HRMS.Domain.Entities.Leave;

namespace HRMS.Application.Leave
{
    public class LeaveOperationResult<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = null!;
        public T? Data { get; set; }

        public static LeaveOperationResult<T> Ok(T data, string message) =>
            new() { Success = true, Message = message, Data = data };

        public static LeaveOperationResult<T> Fail(string message) =>
            new() { Success = false, Message = message };
    }

    public class LeaveBalanceDto
    {
        public LeaveType LeaveType { get; set; }
        public string LeaveTypeName { get; set; } = null!;
        public int Year { get; set; }
        public decimal AllocatedDays { get; set; }
        public decimal CarriedForwardDays { get; set; }
        public decimal UsedDays { get; set; }
        public decimal RemainingDays { get; set; }
        public bool IsUnlimited { get; set; }
    }

    public class LeaveSummaryDto
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = null!;
        public LeaveType LeaveType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsHalfDay { get; set; }
        public decimal DaysCount { get; set; }
        public string? Reason { get; set; }
        public string? AttachmentPath { get; set; }
        public LeaveStatus Status { get; set; }
        public DateTime AppliedAt { get; set; }
        public bool CanCancel { get; set; }
    }

    public class LeaveHistoryItemDto
    {
        public ApprovalStage Stage { get; set; }
        public string ActorName { get; set; } = null!;
        public ApprovalAction Action { get; set; }
        public string? Comments { get; set; }
        public DateTime ActionDate { get; set; }
    }

    public class LeaveDetailsDto
    {
        public LeaveSummaryDto Leave { get; set; } = null!;
        public List<LeaveHistoryItemDto> History { get; set; } = new();
    }

    public class ApplyLeaveRequest
    {
        public int EmployeeId { get; set; }
        public LeaveType LeaveType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsHalfDay { get; set; }
        public string? Reason { get; set; }
        public string? AttachmentPath { get; set; }

        // Maternity-specific
        public DateTime? ExpectedDeliveryDate { get; set; }

        // Overseas-specific
        public string? PassportNumber { get; set; }
        public DateTime? PassportExpiry { get; set; }
        public string? Country { get; set; }
        public string? Purpose { get; set; }
    }
}
