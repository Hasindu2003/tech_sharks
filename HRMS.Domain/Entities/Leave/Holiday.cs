namespace HRMS.Domain.Entities.Leave
{
    public class Holiday
    {
        public int Id { get; set; }

        public DateTime Date { get; set; }
        public string Name { get; set; } = null!;

        // Recurs every year on the same month/day (e.g. fixed public holidays).
        public bool IsRecurringYearly { get; set; }
    }
}
