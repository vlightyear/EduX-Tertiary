using SIS.Models.Registration;

namespace SIS.Models.StudentApplication
{
    public class StudentIdSequence
    {
        public int Id { get; set; } // Primary Key
        public required int AcademicYearId { get; set; } // e.g., "2024"
        public AcademicYear AcademicYear { get; set; } = null!;
        public required int LastGeneratedId { get; set; } // The last generated student ID
    }

}
