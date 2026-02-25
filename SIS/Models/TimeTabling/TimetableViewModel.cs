namespace SIS.Models.TimeTabling
{
    public class TimetableViewModel
    {
        public int AcademicYearId { get; set; }
        public string AcademicYearValue { get; set; }
        public int ModeOfStudyId { get; set; }
        public string ModeOfStudyName { get; set; }
        public List<VenueScheduleViewModel> VenueSchedules { get; set; }
        public List<PeriodData> Periods { get; set; }
        public List<string> WorkingDays { get; set; }
        public string EntityName { get; set; }
        public string UserRole { get; set; }
        public int DepartmentCount { get; set; }
    }
}
