namespace SIS.DTOs
{
    public class EventCreateDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string StartDateTime { get; set; }
        public string EndDateTime { get; set; }
        public bool IsAllDay { get; set; }
        public int AcademicYearId { get; set; }
        public int EventTypeId { get; set; }
        public string Color { get; set; }
        public string Location { get; set; }
        public string ContactPerson { get; set; }
        public string ContactEmail { get; set; }
        public int? SchoolId { get; set; }
        public int? ProgrammeId { get; set; }
        public int? ProgrammeLevelId { get; set; }
        public int? ModeOfStudyId { get; set; }
        public int? StudentYear { get; set; }
        public int? Semester { get; set; }
        public bool IsPublished { get; set; }
        public bool IsSystemEvent { get; set; }
    }
}
