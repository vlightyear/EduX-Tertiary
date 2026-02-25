namespace SIS.Models.Admin
{
    public class TimeSlotConfiguration : AuditClass
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string PeriodsData { get; set; }  // This will store the JSON array of periods
        public bool IsActive { get; set; }
    }
}
