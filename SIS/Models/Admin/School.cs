using SIS.Models.Registration;
using SIS.Models.StudentApplication;
using SIS.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Admin
{
    public class School : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "School name is required.")]
        [StringLength(100)]
        public string Name { get; set; }

        [Required(ErrorMessage = "School description is required.")]
        [StringLength(500)]
        public string Description { get; set; }

        // Dean relationship
        [Display(Name = "Dean")]
        public string? DeanId { get; set; }

        [ForeignKey("DeanId")]
        public ApplicationUser? Dean { get; set; }

        // Assistant Dean relationship
        [Display(Name = "Assistant Dean")]
        public string? AssistantDeanId { get; set; }

        // Assistant Dean relationship
        [Display(Name = "Assistant Registrar")]
        public string? AssistantRegistrarId { get; set; }

        [ForeignKey("AssistantDeanId")]
        public ApplicationUser? AssistantDean { get; set; }

        [ForeignKey("AssistantRegistrarId")]
        public ApplicationUser? AssistantRegistrar { get; set; }

        // Navigation property for Departments
        public ICollection<Department>? Departments { get; set; }

        // Navigation property for Applicants
        public ICollection<Applicant>? Applicants { get; set; }

        public ICollection<AcademicRequest> AcademicRequests { get; set; } = new List<AcademicRequest>();
    }
}