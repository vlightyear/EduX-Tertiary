using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Registration
{
    /// <summary>
    /// A reusable period template — "Semester 1", "Term 2", "Year", etc.
    /// Not tied to any single academic year; multiple years share the same row
    /// via <see cref="AcademicYearPeriod"/> (the join table).
    /// Dates and scheduling details live on <see cref="AcademicYearPeriod"/>, not here.
    /// </summary>
    public class AcademicPeriod
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Human-readable label displayed on screens and reports.
        /// E.g. "Semester 1", "Term 3", "Year".
        /// Must be unique within the same <see cref="AcademicType"/>.
        /// </summary>
        [Required]
        [StringLength(50)]
        [Display(Name = "Period Name")]
        public string PeriodName { get; set; } = string.Empty;

        /// <summary>
        /// 1-based ordering index within its <see cref="AcademicType"/> (1, 2, ... 10).
        /// Drives sort order on timetables, fee screens, and result views.
        /// </summary>
        [Required]
        [Range(1, 10, ErrorMessage = "Period number must be between 1 and 10.")]
        [Display(Name = "Period Number")]
        public int PeriodNumber { get; set; }

        /// <summary>
        /// Constrains which academic-year structures may reference this period.
        /// Prevents accidentally assigning a "Term 1" period to a Semester-type year.
        /// </summary>
        [Required]
        [Display(Name = "Academic Type")]
        public AcademicType AcademicType { get; set; }

        public bool IsActive { get; set; } = true;

        // ── Navigation ───────────────────────────────────────────────────────

        /// <summary>All year-specific schedule entries that use this period definition.</summary>
        public virtual ICollection<AcademicYearPeriod> YearPeriods { get; set; } = new List<AcademicYearPeriod>();
    }
}