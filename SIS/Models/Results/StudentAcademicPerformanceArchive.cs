using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Results
{
    public class StudentAcademicPerformanceArchive : AuditClass
    {
        public int Id { get; set; }

        // Student Information
        public int StudentId { get; set; }
        public string StudentNumber { get; set; }
        public int ProgrammeId { get; set; }

        // Academic Period
        public int AcademicYearId { get; set; }
        public int YearOfStudy { get; set; }  // Study year (1st, 2nd, etc.)

        // Performance Summary
        public int TotalCoursesTaken { get; set; }
        public int CoursesPassed { get; set; }
        public int CoursesFailed { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal GPA { get; set; }
        public string OverallGrade { get; set; }

        // Progression Status
        public string ProgressionStatus { get; set; }  // Progress, Repeat, Exclude, etc.
        public string Remarks { get; set; }

        // Detailed Results (JSON)
        public string CourseResults { get; set; }  // Store detailed course results as JSON

       
    }
}
