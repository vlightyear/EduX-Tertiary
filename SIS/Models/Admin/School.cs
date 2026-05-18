using EduX.Models.GeoPolitical;
using SIS.Data;
using SIS.Models.Registration;
using SIS.Models.StudentApplication;
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
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "School description is required.")]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        // =========================================================
        // Administrative Location Hierarchy
        // =========================================================

        // Nation (Optional)
        [Display(Name = "Nation")]
        public int? NationId { get; set; }

        [ForeignKey(nameof(NationId))]
        public Nation? Nation { get; set; }

        // Province (Optional)
        [Display(Name = "Province")]
        public int? ProvinceId { get; set; }

        [ForeignKey(nameof(ProvinceId))]
        public Province? Province { get; set; }

        // District (Optional)
        [Display(Name = "District")]
        public int? DistrictId { get; set; }

        [ForeignKey(nameof(DistrictId))]
        public District? District { get; set; }

        // Constituency (Optional)
        [Display(Name = "Constituency")]
        public int? ConstituencyId { get; set; }

        [ForeignKey(nameof(ConstituencyId))]
        public Constituency? Constituency { get; set; }

        // Ward (Optional)
        [Display(Name = "Ward")]
        public int? WardId { get; set; }

        [ForeignKey(nameof(WardId))]
        public Ward? Ward { get; set; }

        // =========================================================
        // Administration
        // =========================================================

        // Dean relationship
        [Display(Name = "Dean")]
        public string? DeanId { get; set; }

        [ForeignKey(nameof(DeanId))]
        public ApplicationUser? Dean { get; set; }

        // Assistant Dean relationship
        [Display(Name = "Assistant Dean")]
        public string? AssistantDeanId { get; set; }

        [ForeignKey(nameof(AssistantDeanId))]
        public ApplicationUser? AssistantDean { get; set; }

        // Assistant Registrar relationship
        [Display(Name = "Assistant Registrar")]
        public string? AssistantRegistrarId { get; set; }

        [ForeignKey(nameof(AssistantRegistrarId))]
        public ApplicationUser? AssistantRegistrar { get; set; }

        // =========================================================
        // Navigation Collections
        // =========================================================
        public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();

        // Navigation property for Departments
        public ICollection<Department>? Departments { get; set; }

        // Navigation property for Applicants
        public ICollection<Applicant>? Applicants { get; set; }

        // Navigation property for Academic Requests
        public ICollection<AcademicRequest> AcademicRequests { get; set; } = new List<AcademicRequest>();
    }
}