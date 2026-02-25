using SIS.Models.Registration;

namespace SIS.Models.Admin
{
    public class WorkingDayConfiguration : AuditClass
    {
        public int Id { get; set; }
        public int? AcademicYearId { get; set; }  // Changed from string to int
        public int? ModeOfStudyId { get; set; }   // Changed from string to int
        public string WorkingDaysData { get; set; }
        public bool IsActive { get; set; }

        // Navigation properties
        public virtual AcademicYear AcademicYear { get; set; }
        public virtual ModeOfStudy ModeOfStudy { get; set; }


    }
}