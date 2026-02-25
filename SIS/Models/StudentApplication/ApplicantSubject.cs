using SIS.Models.Registration;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.StudentApplication
{
    public class ApplicantSubject
    {
        public int Id { get; set; }

        public int ApplicantId { get; set; } // Foreign Key

        public string ReferenceNumber { get; set; }

        public int SubjectId { get; set; }

        public int GradeId { get; set; }

        // Navigation properties
        [ForeignKey("ApplicantId")]
        public virtual Applicant Applicant { get; set; } = null!;

        [ForeignKey("SubjectId")]
        public virtual Subject Subject { get; set; } = null!;

        [ForeignKey("GradeId")]
        public virtual Grade Grade { get; set; } = null!;
    }

}
