using SIS.Data;
using SIS.Models.Admin;
using SIS.Models.Registration;
using SIS.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using SIS.Models.Applications;

namespace SIS.Models.Admin
{
    public class Programme : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Programme name is required.")]
        [StringLength(100)]
        public required string Name { get; set; }

        [Required(ErrorMessage = "Programme description is required.")]
        [StringLength(500)]
        public required string Description { get; set; }

        public int DepartmentId { get; set; }
        [ForeignKey("DepartmentId")]
        public Department? Department { get; set; }

        [Required(ErrorMessage = "Minimum points for top 5 subjects is required.")]
        [Range(0, 100)]
        public int MinimumPointsTop5Subjects { get; set; }

        [Required(ErrorMessage = "Programme duration is required.")]
        [Range(1, 10)]
        public int DurationYears { get; set; }

        [Required(ErrorMessage = "Mode of study is required.")]
        public int ModeOfStudyId { get; set; }
        [ForeignKey("ModeOfStudyId")]
        public ModeOfStudy? ModeOfStudy { get; set; }

        [Required(ErrorMessage = "Programme level is required.")]
        public int ProgrammeLevelId { get; set; }
        [ForeignKey("ProgrammeLevelId")]
        public ProgramLevel? ProgrammeLevel { get; set; }

        [Required(ErrorMessage = "Programme coordinator is required.")]
        public required string CoordinatorId { get; set; }
        [ForeignKey("CoordinatorId")]
        public ApplicationUser? Coordinator { get; set; }

        public int EnrollmentCount { get; set; } = 0;  // Default to 0

        // Indicates if the programme is semester-based
        public bool IsSemesterBased { get; set; } = false;

        /// <summary>
        /// Academic period structure for this programme.
        /// Determines how courses are split across periods within each year.
        /// </summary>
        public AcademicType AcademicType { get; set; } = AcademicType.Semester;

        public string YearlyRequirements { get; set; } = "{}";  // Default empty JSON object

        
        // FIELDS FOR NON-QUOTA SUPPORT
        /// <summary>
        /// Indicates if this programme is a Non-Quota (NQ) programme
        /// </summary>
        public bool IsNonQuota { get; set; } = false;

        /// <summary>
        /// Foreign key to the associated Non-Quota programme (if this is a regular programme)
        /// This links a regular programme to its corresponding NQ programme
        /// </summary>
        public int? AssociatedNQProgrammeId { get; set; }

        /// <summary>
        /// Navigation property to the associated Non-Quota programme
        /// </summary>
        [ForeignKey("AssociatedNQProgrammeId")]
        public Programme? AssociatedNQProgramme { get; set; }

        /// <summary>
        /// Navigation property for programmes that are associated with this NQ programme
        /// (Inverse relationship - if this is an NQ programme, this collection contains regular programmes linked to it)
        /// </summary>
        [InverseProperty("AssociatedNQProgramme")]
        public ICollection<Programme> AssociatedRegularProgrammes { get; set; } = new List<Programme>();

        //  NAVIGATION PROPERTIES
        public ICollection<ProgrammeCourse> ProgrammeCourses { get; set; } = new List<ProgrammeCourse>();
        public ICollection<AcademicRequest> AcademicRequests { get; set; } = new List<AcademicRequest>();


        public int? ApplicationPeriodId {  get; set; }
        [ForeignKey(nameof(ApplicationPeriodId))]
        public ApplicationPeriod ApplicationPeriod {  get; set; }
    }
}