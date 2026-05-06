using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Registration
{
    /// <summary>
    /// Join table between <see cref="AcademicYear"/> and <see cref="AcademicPeriod"/>.
    /// Carries all year-specific scheduling data (dates, windows) for one period
    /// within one academic year.
    ///
    /// Example rows:
    ///   AcademicYear 2024/2025  +  AcademicPeriod "Semester 1"  →  Jan–Jun 2025
    ///   AcademicYear 2025/2026  +  AcademicPeriod "Semester 1"  →  Jan–Jun 2026
    ///   AcademicYear 2025/2026  +  AcademicPeriod "Semester 2"  →  Jul–Dec 2026
    ///
    /// Student, FeeConfiguration, and StudentDisqualification all FK into this table
    /// to reference a specific year+period combination.
    /// </summary>
    public class AcademicYearPeriod
    {
        [Key]
        public int Id { get; set; }

        // ── Keys ─────────────────────────────────────────────────────────────

        [Required]
        public int AcademicYearId { get; set; }

        [ForeignKey(nameof(AcademicYearId))]
        public virtual AcademicYear AcademicYear { get; set; } = null!;

        [Required]
        public int AcademicPeriodId { get; set; }

        [ForeignKey(nameof(AcademicPeriodId))]
        public virtual AcademicPeriod AcademicPeriod { get; set; } = null!;

        // ── Core date range ───────────────────────────────────────────────────

        [Required]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; }

        // ── Optional scheduling windows ───────────────────────────────────────

        [Display(Name = "Exam Start Date")]
        public DateTime? ExamStartDate { get; set; }

        [Display(Name = "Exam End Date")]
        public DateTime? ExamEndDate { get; set; }

        [Display(Name = "Registration Start Date")]
        public DateTime? RegistrationStartDate { get; set; }

        [Display(Name = "Registration End Date")]
        public DateTime? RegistrationEndDate { get; set; }

        [Display(Name = "Grade Submission Start Date")]
        public DateTime? GradeSubmissionStartDate { get; set; }

        [Display(Name = "Grade Submission End Date")]
        public DateTime? GradeSubmissionEndDate { get; set; }

        /// <summary>
        /// True when this year+period combination is the current active one.
        /// Only one row per academic year should be active at any time.
        /// </summary>
        public bool IsActive { get; set; }

        // ── Computed helpers ──────────────────────────────────────────────────

        [NotMapped]
        public bool IsCurrentlyRunning =>
            DateTime.UtcNow >= StartDate && DateTime.UtcNow <= EndDate;

        [NotMapped]
        public bool IsInRegistrationWindow =>
            RegistrationStartDate.HasValue && RegistrationEndDate.HasValue &&
            DateTime.UtcNow >= RegistrationStartDate.Value &&
            DateTime.UtcNow <= RegistrationEndDate.Value;

        [NotMapped]
        public bool IsInExamWindow =>
            ExamStartDate.HasValue && ExamEndDate.HasValue &&
            DateTime.Now >= ExamStartDate.Value &&
            DateTime.Now <= ExamEndDate.Value;

        /// <summary>
        /// E.g. "2024/2025 – Semester 1". Requires <see cref="AcademicYear"/>
        /// and <see cref="AcademicPeriod"/> to be loaded.
        /// </summary>
        [NotMapped]
        public string FullLabel =>
            $"{AcademicYear?.YearValue} – {AcademicPeriod?.PeriodName}";

        /// <summary>
        /// Shortcut to the period's display name without needing to navigate twice.
        /// </summary>
        [NotMapped]
        public string PeriodName => AcademicPeriod?.PeriodName ?? string.Empty;

        /// <summary>
        /// Shortcut to the period's ordering number.
        /// </summary>
        [NotMapped]
        public int PeriodNumber => AcademicPeriod?.PeriodNumber ?? 0;
    }
}