namespace SIS.Models.Admin
{
    public class DayConfiguration
    {
        public string Day { get; set; }  // Monday, Tuesday, etc.
        public bool IsWorkingDay { get; set; }
        public int? TimeSlotConfigId { get; set; }  // Nullable since non-working days won't have a time slot
    }
}