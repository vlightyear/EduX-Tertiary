namespace SIS.Models.TimeTabling
{
    public class VenueScheduleViewModel
    {
        public int VenueId { get; set; }
        public string VenueName { get; set; }
        public Dictionary<string, Dictionary<int, List<TimetableSessionViewModel>>> DayPeriodSessions { get; set; }
    }
}
