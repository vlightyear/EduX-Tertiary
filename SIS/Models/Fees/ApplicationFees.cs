using SIS.Models.Registration;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Fees
{
    public class ApplicationFees : AuditClass
    {
        [Key]
        public int Id { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public required decimal Amount { get; set; }
        public required string Name { get; set; }
        public required string ProgrammeLevel{ get; set; }
        public int EffectiveYearId { get; set; }
        public AcademicYear AcademicYear { get; set; }
    }
}
