using SIS.Models.StudentApplication;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using SIS.Models.Registration;

namespace SIS.Models.Admin
{
    public class StudentCourseRegistration
    {
        [Key]
        public int Id { get; set; }

        public int StudentId { get; set; }
        [ForeignKey("StudentId")]
        public Student Student { get; set; }

        public int CourseId { get; set; }
        [ForeignKey("CourseId")]
        public Course Course { get; set; }

        public int AcademicYearId { get; set; }  // New foreign key property
        [ForeignKey("AcademicYearId")]
        public AcademicYear AcademicYear { get; set; }  // New navigation property

        public int YearPeriodId { get; set; }
        public DateTime RegistrationDate { get; set; }
    }
}
