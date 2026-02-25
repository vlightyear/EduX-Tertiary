using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Fees
{
    public class FeeType : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(50)]
        public string ApplicableFor { get; set; }

        public bool IsActive { get; set; }
    }
}
