using SIS.Enums;
using SIS.Models.Registration;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Admin
{
    public class AcademicCalendarEvent : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Event Title")]
        public string Title { get; set; }

        [StringLength(2000)]
        [Display(Name = "Event Description")]
        public string Description { get; set; }

        [Required]
        [Display(Name = "Start Date/Time")]
        public DateTime StartDateTime { get; set; }

        [Display(Name = "End Date/Time")]
        public DateTime? EndDateTime { get; set; }

        [Display(Name = "All Day Event")]
        public bool IsAllDay { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Event Color")]
        public string Color { get; set; } = "#3788d8"; // Default blue color

        // System events are auto-generated from academic year data and cannot be edited/deleted
        [Display(Name = "System Event")]
        public bool IsSystemEvent { get; set; } = false;

        // Identifies published events visible to students/staff
        [Display(Name = "Published")]
        public bool IsPublished { get; set; } = true;

        // Foreign key for event type
        [Required]
        [Display(Name = "Event Type")]
        public int EventTypeId { get; set; }

        // Navigation property for event type
        [ForeignKey("EventTypeId")]
        public virtual AcademicEventType EventType { get; set; }

        // Foreign key for academic year
        [Required]
        [Display(Name = "Academic Year")]
        public int AcademicYearId { get; set; }

        // Navigation property for academic year
        [ForeignKey("AcademicYearId")]
        public virtual AcademicYear AcademicYear { get; set; }

        // Optional filters for targeting specific groups

        // School filter
        [Display(Name = "School")]
        public int? SchoolId { get; set; }

        [ForeignKey("SchoolId")]
        public virtual School School { get; set; }

        // Programme filter
        [Display(Name = "Programme")]
        public int? ProgrammeId { get; set; }

        [ForeignKey("ProgrammeId")]
        public virtual Programme Programme { get; set; }

        // Programme level filter
        [Display(Name = "Programme Level")]
        public int? ProgrammeLevelId { get; set; }

        [ForeignKey("ProgrammeLevelId")]
        public virtual ProgramLevel ProgrammeLevel { get; set; }

        // Mode of study filter
        [Display(Name = "Mode of Study")]
        public int? ModeOfStudyId { get; set; }

        [ForeignKey("ModeOfStudyId")]
        public virtual ModeOfStudy ModeOfStudy { get; set; }

        // Student year filter
        [Display(Name = "Student Year")]
        public int? StudentYear { get; set; }

        // Semester filter
        [Display(Name = "Semester")]
        public int? Semester { get; set; }

        // Additional metadata

        // Location of the event (optional)
        [StringLength(200)]
        [Display(Name = "Location")]
        public string Location { get; set; }

        // Contact person (optional)
        [StringLength(100)]
        [Display(Name = "Contact Person")]
        public string ContactPerson { get; set; }

        // Contact email (optional)
        [StringLength(100)]
        [Display(Name = "Contact Email")]
        [EmailAddress]
        public string ContactEmail { get; set; }
    }
}