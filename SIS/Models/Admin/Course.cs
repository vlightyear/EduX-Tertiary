using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Fees;
using SIS.Models.Lecturer;
using SIS.Models.Registration;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Admin
{
    public class Course : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Course code is required.")]
        [StringLength(50)]
        [Display(Name = "Course Code")]
        public string CourseCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Course name is required.")]
        [StringLength(100)]
        [Display(Name = "Course Name")]
        public string CourseName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Course type is required.")]
        [Display(Name = "Course Type")]
        public string CourseType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Course description is required.")]
        [StringLength(500)]
        [Display(Name = "Course Description")]
        public string CourseDescription { get; set; } = string.Empty;

        // ── Academic placement ────────────────────────────────────────────────

        [Required(ErrorMessage = "Programme is required.")]
        [Display(Name = "Programme")]
        public int ProgrammeID { get; set; }

        [ForeignKey(nameof(ProgrammeID))]
        [DeleteBehavior(DeleteBehavior.NoAction)]
        public virtual Programme Programme { get; set; } = null!;

        [Required(ErrorMessage = "Pass mark is required.")]
        [Range(0, 100)]
        [Display(Name = "Pass Mark")]
        public double PassMark { get; set; }

        [Required(ErrorMessage = "Year taken is required.")]
        [Range(1, 6)]
        [Display(Name = "Year Taken")]
        public int YearTaken { get; set; }

        // ── Period placement ──────────────────────────────────────────────────

        /// <summary>
        /// FK to the reusable <see cref="AcademicPeriod"/> template in which this course
        /// is taught (e.g. "Semester 1", "Term 2").
        /// Points at the period definition, not a year-specific instance — because
        /// a course's place in the curriculum doesn't change year to year.
        /// </summary>
        [Required(ErrorMessage = "Period taken is required.")]
        [Display(Name = "Period Taken")]
        public int PeriodTakenId { get; set; }

        [ForeignKey(nameof(PeriodTakenId))]
        public virtual AcademicPeriod PeriodTaken { get; set; } = null!;

        [NotMapped]
        public string PeriodTakenLabel => PeriodTaken?.PeriodName ?? string.Empty;

        // ── Course settings ───────────────────────────────────────────────────

        [Required]
        [Display(Name = "Is Mandatory")]
        public bool IsMandatory { get; set; }

        [Required]
        [Display(Name = "Is Examinable")]
        public bool IsExaminable { get; set; } = true;

        // ── Scheduling ────────────────────────────────────────────────────────

        [Required(ErrorMessage = "Instructor is required.")]
        public string? InstructorId { get; set; }

        [Required]
        [Range(1, 7)]
        [Display(Name = "Meetings Per Week")]
        public int MeetingFrequencyPerWeek { get; set; }

        [Required]
        [Display(Name = "Capacity Required")]
        public int CapacityRequired { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        [Display(Name = "Preferred Venues")]
        public string? PreferredVenueIds { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        [Display(Name = "Prerequisite Courses")]
        public string? PrerequisiteCourseIds { get; set; }

        [NotMapped]
        public int Credits { get; set; } = 3;

        // ── Navigation ────────────────────────────────────────────────────────

        public virtual ICollection<CourseLecturer> CourseLecturers { get; set; } = new List<CourseLecturer>();
        public virtual ICollection<CoursePrerequisite> Prerequisites { get; set; } = new List<CoursePrerequisite>();
        public virtual ICollection<CourseAssessment> CourseAssessments { get; set; } = new List<CourseAssessment>();
        public virtual ICollection<ChapterProgress> ChapterProgresses { get; set; } = new List<ChapterProgress>();
    }
}