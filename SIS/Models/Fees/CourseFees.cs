using SIS.Models.Admin;
using SIS.Models.Registration;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Fees
{
    public class CourseFees : AuditClass
    {
        [Key]
        public int Id { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; set; }

        public int YearId { get; set; }
        [ForeignKey("YearId")]
        public required AcademicYear Year { get; set; }

        // Foreign key for Course
        public int CourseId { get; set; }
        [ForeignKey("CourseId")]
        public required Course Course { get; set; }
    }
}


