using System;

namespace HRMS.Domain.Entities.Welfare
{
    public class WelfareType
    {
        public int WelfareTypeId { get; set; }          // Primary Key (welfare_type_id)
        public string TypeName { get; set; } = null!;   // type_name
        public string? Category { get; set; }           // category
        public decimal MaxEligibleAmount { get; set; }  // max_eligible_amount
        public bool IsActive { get; set; } = true;      // is_active
        public DateTime CreatedAt { get; set; }         // created_at

        // Navigation property
        public ICollection<WelfareRequest> WelfareRequests { get; set; } = new List<WelfareRequest>();
    }
}