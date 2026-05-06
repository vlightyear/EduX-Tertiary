using SIS.Models.Accounts;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Registration
{
    public enum AcademicType
    {
        /// <summary>One period spanning the full academic year.</summary>
        Annual,

        /// <summary>Two periods — Semester 1 and Semester 2.</summary>
        Semester,

        /// <summary>Three periods — Term 1, Term 2, and Term 3.</summary>
        Term
    }

    public static class AcademicTypeExtensions
    {
        /// <summary>Returns how many periods this academic type produces.</summary>
        public static int PeriodCount(this AcademicType type) => type switch
        {
            AcademicType.Annual => 1,
            AcademicType.Semester => 2,
            AcademicType.Term => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        /// <summary>Returns the label for a given 1-based period number.</summary>
        public static string PeriodLabel(this AcademicType type, int periodNumber) => type switch
        {
            AcademicType.Annual => "Year",
            AcademicType.Semester => $"Semester {periodNumber}",
            AcademicType.Term => $"Term {periodNumber}",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    public class AcademicYear
    {
        [Key]
        public int YearId { get; set; }

        [Required]
        public string YearValue { get; set; } = string.Empty; // e.g. "2024" or "2024/2025"

        [NotMapped]
        public string YearName => YearValue;

        [Required]
        public AcademicType AcademicType { get; set; } = AcademicType.Semester;

        [Required]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; }

        // ── Payment thresholds ────────────────────────────────────────────────

        [Range(0, 100)]
        [Display(Name = "Minimum Payment % for Registration")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal MinRegistrationPaymentPercentage { get; set; }

        [Range(0, 100)]
        [Display(Name = "Minimum Payment % for Exams")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal MinExamPaymentPercentage { get; set; }

        // ── Optional year-level registration window ───────────────────────────

        [Display(Name = "Registration Start Date")]
        public DateTime? RegistrationStartDate { get; set; }

        [Display(Name = "Registration End Date")]
        public DateTime? RegistrationEndDate { get; set; }

        // ── Progression chain ─────────────────────────────────────────────────

        [Display(Name = "Next Academic Year")]
        public int? NextAcademicYearId { get; set; }

        [ForeignKey(nameof(NextAcademicYearId))]
        public AcademicYear? NextAcademicYear { get; set; }

        [InverseProperty(nameof(NextAcademicYear))]
        public ICollection<AcademicYear> PreviousAcademicYears { get; set; } = new HashSet<AcademicYear>();

        // ── Periods (via join table) ──────────────────────────────────────────

        /// <summary>
        /// The scheduled periods for this academic year.
        /// Each entry links to a reusable <see cref="AcademicPeriod"/> definition
        /// and carries the year-specific date windows.
        /// </summary>
        public virtual ICollection<AcademicYearPeriod> YearPeriods { get; set; } = new List<AcademicYearPeriod>();

        // ── Navigation ────────────────────────────────────────────────────────

        public ICollection<FinancialStatement> FinancialStatements { get; set; } = new HashSet<FinancialStatement>();

        // ── Computed helpers ──────────────────────────────────────────────────

        [NotMapped]
        public AcademicYearPeriod? ActiveYearPeriod =>
            YearPeriods.FirstOrDefault(yp => yp.IsActive);

        [NotMapped]
        public int ExpectedPeriodCount => AcademicType.PeriodCount();

        // ── Deprecated — remove after migration is complete ───────────────────

        [Obsolete("Use YearPeriods collection. Will be removed after migration.")]
        public int? SemesterId { get; set; }

        [Obsolete("Use YearPeriods collection. Will be removed after migration.")]
        public int? ModeId { get; set; }
    }
}