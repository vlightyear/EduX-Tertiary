namespace SIS.Models.TimeTabling
{
    public class TimetableGroupViewModel
    {
        public int AcademicYearId { get; set; }
        public string AcademicYearValue { get; set; }
        public int ModeOfStudyId { get; set; }
        public string ModeOfStudyName { get; set; }
        public int TimetableCount { get; set; }
        public int DraftCount { get; set; }
        public int PublishedCount { get; set; }
        public bool HasCurrentSemesterTimetables { get; set; }
        public DateTime LatestUpdate { get; set; }
    }
}
