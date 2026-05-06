using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SIS.Models.Admin;
using SIS.Models.Registration;

namespace SIS.Models.Fees
{
    public class FeeConfiguration : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(AcademicYear))]
        public int? AcademicYearId { get; set; }

        [ForeignKey(nameof(School))]
        public int? SchoolId { get; set; }

        [ForeignKey(nameof(Programme))]
        public int? ProgrammeId { get; set; }

        [ForeignKey(nameof(ModeOfStudy))]
        public int? ModeOfStudyId { get; set; }

        public int? YearOfStudy { get; set; }

        [ForeignKey(nameof(FeeType))]
        [Required]
        public int FeeTypeId { get; set; }

        [ForeignKey(nameof(ProgramLevel))]
        public int? ProgramLevelId { get; set; }

        // ── Period scoping ────────────────────────────────────────────────────

        /// <summary>
        /// FK to a specific <see cref="AcademicYearPeriod"/> (year + period combination).
        /// Null  = this fee applies to every period in <see cref="AcademicYearId"/> (year-wide fee).
        /// Set   = this fee applies only during that one year+period slot.
        ///
        /// Note: <see cref="AcademicYearPeriod"/> already knows its year, so when this
        /// is non-null you don't strictly need <see cref="AcademicYearId"/> — but keeping
        /// it allows efficient year-only queries without a join.
        /// </summary>
        [ForeignKey(nameof(YearPeriod))]
        [Display(Name = "Academic Period")]
        public int? YearPeriodId { get; set; }

        public virtual AcademicYearPeriod? YearPeriod { get; set; }

        // ── Amounts ───────────────────────────────────────────────────────────

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public decimal RegistrationPaymentRequired { get; set; } = 75;

        // ── Applicability flags ───────────────────────────────────────────────

        [Required] public bool AppliesOnlyToAccommodated { get; set; }
        [Required] public bool AppliesUniversally { get; set; }
        [Required] public bool AppliesOnlyToForeignStudents { get; set; }
        [Required] public bool AppliesOnlyToLocalStudents { get; set; }

        public bool IsActive { get; set; } = true;

        // ── Accounting codes ──────────────────────────────────────────────────

        [Required(ErrorMessage = "Credit N-Code is required.")]
        [StringLength(20)]
        public required string CreditNCode { get; set; }

        [Required(ErrorMessage = "Debit N-Code is required.")]
        [StringLength(20)]
        public required string DebitNCode { get; set; }

        // ── Navigation ────────────────────────────────────────────────────────

        public virtual AcademicYear? AcademicYear { get; set; }
        public virtual School? School { get; set; }
        public virtual Programme? Programme { get; set; }
        public virtual ModeOfStudy? ModeOfStudy { get; set; }
        public virtual FeeType FeeType { get; set; } = null!;
        public virtual ProgramLevel? ProgramLevel { get; set; }

        // ── Computed helpers ──────────────────────────────────────────────────

        [NotMapped]
        public bool IsYearWideFee => YearPeriodId is null;

        [NotMapped]
        public string PeriodScopeLabel =>
            YearPeriod is not null ? YearPeriod.FullLabel : "All Periods";
    }
}