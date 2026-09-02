using System;
using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Training;

namespace HRMS.Domain.Entities.Calendar
{
    public class CalendarEvent
    {
        public int Id { get; set; }

        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        
        // Event Type: "Meeting", "Personal", "Reminder", "Event", "Holiday", "Training"
        public string EventType { get; set; } = "Event";

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsAllDay { get; set; } = false;

        public string? Location { get; set; }
        public string? MeetingLink { get; set; }

        // Creator Identity User ID
        public string CreatedByUserId { get; set; } = null!;

        // Optional Association with a specific Employee
        public int? EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        // Optional Branch scope (null = all branches / global)
        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }

        // Optional Department scope
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        // Optional Reference to a Training session
        public int? TrainingId { get; set; }
        public HRMS.Domain.Entities.Training.Training? Training { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Reminder notification tracking
        public bool DayBeforeNotificationSent { get; set; } = false;
        public bool HourBeforeNotificationSent { get; set; } = false;
    }
}
