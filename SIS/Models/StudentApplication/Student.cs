using Microsoft.EntityFrameworkCore;
using SIS.Enums;
using SIS.Models.Accounts;
using SIS.Models.Admin;
using SIS.Models.Appeals;
using SIS.Models.Lecturer;
using SIS.Models.Payments;
using SIS.Models.Registration;
using SIS.Models.StudentAccommodation;
using SIS.Models.StudyPermits;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.StudentApplication
{
    public class Student : AuditClass
    {
        public int Id { get; set; }

        // ── Personal details ──────────────────────────────────────────────────

        public bool IsForeigner { get; set; }
        public required string ApplicationReferenceNumber { get; set; }
        public required string FullName { get; set; }
        public required DateTime DateOfBirth { get; set; }
        public required string Gender { get; set; }
        public required string Phone { get; set; }
        public required string Email { get; set; }
        public required string MaritalStatus { get; set; }
        public required string Nationality { get; set; }
        public required string Religion { get; set; }
        public required string NrcOrPassportNumber { get; set; }
        public required string NrcOrPassportCopy { get; set; }

        // ── Identity & status ─────────────────────────────────────────────────

        public string? StudyPermission { get; set; }
        public required string StudentId_Number { get; set; }
        public required string Username { get; set; }
        public required Status StudentStatus { get; set; }
        public bool IsAdmitted { get; set; } = false;
        public DateTime? AdmissionDate { get; set; }
        public bool IsRegistered { get; set; }
        public Status RegistrationStatus { get; set; }
        public DateTime? RegistrationDate { get; set; }

        /// <summary>Year of study the student is currently in (1st year, 2nd year, etc.).</summary>
        public int? StudentCurrentYear { get; set; }

        // ── Current period ────────────────────────────────────────────────────

        /// <summary>
        /// FK to the <see cref="AcademicYearPeriod"/> the student is currently enrolled in.
        /// This is a year+period combination (e.g. "2025/2026 – Semester 1"), not just a
        /// period template — so it already implies the academic year.
        /// Null = not currently assigned to an active period.
        /// </summary>
        [Display(Name = "Current Period")]
        public int? CurrentYearPeriodId { get; set; }

        [ForeignKey(nameof(CurrentYearPeriodId))]
        public virtual AcademicYearPeriod? CurrentYearPeriod { get; set; }

        /// <summary>Convenience accessor — returns the period number (1, 2, 3) or null.</summary>
        [NotMapped]
        public int? CurrentPeriodNumber => CurrentYearPeriod?.PeriodNumber;

        /// <summary>Returns a label such as "Semester 1" or "Term 2".</summary>
        [NotMapped]
        public string CurrentPeriodLabel => CurrentYearPeriod?.PeriodName ?? "—";

        /// <summary>Returns the full label, e.g. "2025/2026 – Semester 1".</summary>
        [NotMapped]
        public string CurrentYearPeriodLabel => CurrentYearPeriod?.FullLabel ?? "—";

        // ── Programme / enrolment FKs ─────────────────────────────────────────

        public int ProgrammeLevelId { get; set; }
        public ProgramLevel ProgrammeLevel { get; set; } = null!;

        public int SchoolId { get; set; }
        public School School { get; set; } = null!;

        public int ProgrammeId { get; set; }
        public Programme Programme { get; set; } = null!;

        public int ModeOfStudyId { get; set; }
        public ModeOfStudy ModeOfStudy { get; set; } = null!;

        public int AcademicYearId { get; set; }
        public AcademicYear AcademicYear { get; set; } = null!;

        // ── Address / kin / former school ─────────────────────────────────────

        public int? AddressId { get; set; }
        public StudentAddress? StudentAddress { get; set; }

        public int? NextOfKinId { get; set; }
        public StudNextOfKin? NextOfKin { get; set; }

        public int? FormerSchoolId { get; set; }
        public StudFormerSchool? FormerSchool { get; set; }

        // ── Grades ────────────────────────────────────────────────────────────

        public ICollection<CourseGrades> CourseGrades { get; set; } = new List<CourseGrades>();
        public ICollection<StudentGceSubjects> SubjectGrades { get; set; } = new List<StudentGceSubjects>();

        // ── Finance ───────────────────────────────────────────────────────────

        [Column(TypeName = "decimal(18,2)")]
        public decimal OutstandingFees { get; set; }
        public ICollection<FinancialStatement> FinancialStatements { get; set; } = new List<FinancialStatement>();

        // ── Requests ──────────────────────────────────────────────────────────

        public ICollection<AcademicRequest> AcademicRequests { get; set; } = new List<AcademicRequest>();

        // ── Accommodation ─────────────────────────────────────────────────────

        public bool HasAccommodationClearance { get; set; } = false;
        public int? BedId { get; set; }
        [ForeignKey(nameof(BedId))]
        public BedSpace BedSpace { get; set; } = null!;
        public DateTime? BedAllocationEndDate { get; set; }
        public bool IsBlackListedFromAccommodation { get; set; }
        public string? BlackListedFromAccommodationReason { get; set; }

        // ── Payment statuses ──────────────────────────────────────────────────

        public bool HasPaidFullFees { get; set; } = false;
        public bool HasPaid75PercentFees { get; set; } = false;

        // ── Documents & access ────────────────────────────────────────────────

        public string? PassportPhotoPath { get; set; }
        public DateTime? IdCardPrintedDate { get; set; }
        public string? ClassPass { get; set; }

        // ── Collections ───────────────────────────────────────────────────────

        public ICollection<Course> RegisteredCourses { get; set; } = new List<Course>();
        public virtual ICollection<ChapterProgress> ChapterProgresses { get; set; } = new List<ChapterProgress>();
        public virtual List<StudyPermit> StudyPermits { get; set; } = new();
        public virtual List<OnlinePayments> OnlinePayments { get; set; } = new();
        public List<StudentDisqualification> DisQulifications { get; set; } = new();
        public List<ResultAppeal> ResultAppeals { get; set; } = new();
    }

    public class StudentDisqualification : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Student is required.")]
        public int StudentId { get; set; }
        [ForeignKey(nameof(StudentId))]
        public Student? Student { get; set; }

        [Required(ErrorMessage = "Course is required.")]
        public int CourseId { get; set; }
        [ForeignKey(nameof(CourseId))]
        public Course? Course { get; set; }

        [Required(ErrorMessage = "Academic Year is required.")]
        public int AcademicYearId { get; set; }
        [ForeignKey(nameof(AcademicYearId))]
        public AcademicYear? AcademicYear { get; set; }

        // ── Period reference ──────────────────────────────────────────────────

        /// <summary>
        /// FK to the specific <see cref="AcademicYearPeriod"/> in which the incident occurred.
        /// Using the year+period combination gives us both the period name and the exact
        /// date range, which is useful for audit trails.
        /// </summary>
        [Required(ErrorMessage = "Academic period is required.")]
        [Display(Name = "Academic Period")]
        public int YearPeriodId { get; set; }

        [ForeignKey(nameof(YearPeriodId))]
        public virtual AcademicYearPeriod? YearPeriod { get; set; }

        [NotMapped]
        public string PeriodLabel => YearPeriod?.FullLabel ?? "—";

        // ── Disqualification details ──────────────────────────────────────────

        [Required]
        [StringLength(100)]
        public string DisqualificationType { get; set; } = "Malpractice";

        [Required]
        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [StringLength(500)]
        public string? EvidenceReference { get; set; }

        [Required]
        public DateTime IncidentDate { get; set; }

        [Required]
        public DateTime DisqualificationDate { get; set; } = DateTime.Now.AddHours(2);

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Pending";

        // ── Penalty ───────────────────────────────────────────────────────────

        [StringLength(500)]
        public string? PenaltyDescription { get; set; }
        public int? PenaltyDurationSemesters { get; set; }
        public bool IsBannedFromCourse { get; set; } = false;
        public bool IsSuspended { get; set; } = false;
        public int YearsSuspended { get; set; } = 2;

        // ── Appeal ────────────────────────────────────────────────────────────

        public DateTime? AppealDate { get; set; }
        [StringLength(2000)] public string? AppealDescription { get; set; }
        [StringLength(50)] public string? AppealStatus { get; set; }
        [StringLength(2000)] public string? AppealDecision { get; set; }
        public DateTime? AppealDecisionDate { get; set; }

        // ── Resolution ────────────────────────────────────────────────────────

        public DateTime? ResolvedDate { get; set; }
        [StringLength(2000)] public string? ResolutionNotes { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}