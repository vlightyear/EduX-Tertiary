using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Registration
{
    public class Grade : AuditClass
    {
        [Key]
        public int GradeId { get; set; } // Primary Key
        public required string GradeValue { get; set; } // like One, Two, Three
        public required int GradePoint { get; set; } // like 1, 2, 3
        public required string Description { get; set; } // like Distinction, Merit, Credit
        public required string Code { get; set; } // like A+, A, B+, B
    }
}
