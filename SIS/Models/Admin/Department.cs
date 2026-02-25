using SIS.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Admin
{
    public class Department : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Display(Name = "Department Name")]
        public required string Name { get; set; }

        // Field for HOD (Head of Department)
        [Required]
        [Display(Name = "HOD")]
        public required string HODId { get; set; }

        [ForeignKey("HODId")]
        public ApplicationUser? HOD { get; set; }

        [MaxLength(250)]
        [Display(Name = "Description")]
        public required string? Description { get; set; }

        [Required]
        [Display(Name = "Is Active")]
        public required bool IsActive { get; set; } = true;

        // Navigation property for Programmes
        public ICollection<Programme>? Programmes { get; set; }

        // Foreign key for School
        public int SchoolId { get; set; }
        [ForeignKey("SchoolId")]
        public required School School { get; set; }
    }
}
