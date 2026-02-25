using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Registration
{
    public class Subject : AuditClass
    {
        [Key]
        public int SubjectId { get; set; } // Primary Key
        public required string SubjectName { get; set; }
        public required string SubjectCode { get; set; }
    }
}
