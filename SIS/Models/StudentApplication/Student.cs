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

        // Personal details
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

        // Student Identity Details
        public string? StudyPermission { get; set; }  // Study Permit for International Students
        public required string StudentId_Number { get; set; } // Unique student ID
        public required string Username { get; set; }
        public required Status StudentStatus { get; set; }
        public bool IsAdmitted { get; set; } = false;
        public DateTime? AdmissionDate { get; set; }
        public bool IsRegistered { get; set; }
        public Status RegistrationStatus { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public int? StudentCurrentYear { get; set; }
        public int? CurrentSemester { get; set; }

        // Foreign Key Properties - Relationships to other tables
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

        public int? AddressId { get; set; }
        public StudentAddress? StudentAddress { get; set; }

        public int? NextOfKinId { get; set; }
        public StudNextOfKin? NextOfKin { get; set; }

        public int? FormerSchoolId { get; set; }
        public StudFormerSchool? FormerSchool { get; set; }

        // Navigation properties for SubjectGrades
        public ICollection<CourseGrades> CourseGrades { get; set; } = new List<CourseGrades>();
        public ICollection<StudentGceSubjects> SubjectGrades { get; set; } = new List<StudentGceSubjects>();

        // Financial Management
        [Column(TypeName = "decimal(18,2)")]
        public decimal OutstandingFees { get; set; }  // Track outstanding fees
        public ICollection<FinancialStatement> FinancialStatements { get; set; } = new List<FinancialStatement>();

        // Requests
        public ICollection<AcademicRequest> AcademicRequests { get; set; } = new List<AcademicRequest>();

        // Foreign students specific
        public bool HasAccommodationClearance { get; set; } = false;  // Track accommodation clearance status
        public int? BedId { get; set; }
        [ForeignKey(nameof(BedId))]
        public BedSpace BedSpace { get; set; }
        public DateTime? BedAllocationEndDate {  get; set; }
        public bool IsBlackListedFromAccommodation {  get; set; }
        public string? BlackListedFromAccommodationReason { get; set; }

        // Payment Statuses and Conditions
        public bool HasPaidFullFees { get; set; } = false;
        public bool HasPaid75PercentFees { get; set; } = false;

        public string? PassportPhotoPath { get; set; }
        public DateTime? IdCardPrintedDate { get; set; }

        // Registration-related process
        public string? ClassPass { get; set; }  // Class pass for the student
        public ICollection<Course> RegisteredCourses { get; set; } = new List<Course>();
        public virtual ICollection<ChapterProgress> ChapterProgresses { get; set; }
        public virtual List<StudyPermit> StudyPermits { get; set; }
        public virtual List<OnlinePayments> OnlinePayments { get; set; } = new();
        public List<StudentDisqualification> DisQulifications { get; set; }= new();
        public List<ResultAppeal> ResultAppeals { get; set; } = new();
    }

    public class StudentDisqualification : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Student is required.")]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public Student? Student { get; set; }

        [Required(ErrorMessage = "Course is required.")]
        public int CourseId { get; set; }

        [ForeignKey("CourseId")]
        public Course? Course { get; set; }

        [Required(ErrorMessage = "Academic Year is required.")]
        public int AcademicYearId { get; set; }

        [ForeignKey("AcademicYearId")]
        public AcademicYear? AcademicYear { get; set; }

        [Required(ErrorMessage = "Semester is required.")]
        [Range(1, 2, ErrorMessage = "Semester must be 1 or 2.")]
        public int Semester { get; set; }

        [Required(ErrorMessage = "Disqualification type is required.")]
        [StringLength(100)]
        public string DisqualificationType { get; set; } = "Malpractice";

        [Required(ErrorMessage = "Description is required.")]
        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [StringLength(500)]
        public string? EvidenceReference { get; set; }

        [Required]
        public DateTime IncidentDate { get; set; }

        [Required]
        public DateTime DisqualificationDate { get; set; } = DateTime.Now.AddHours(2);

        // Status: Pending, Confirmed, Appealed, Overturned, Completed
        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Pending";

        // Penalty details
        [StringLength(500)]
        public string? PenaltyDescription { get; set; }

        // Duration of penalty in semesters (if applicable)
        public int? PenaltyDurationSemesters { get; set; }

        // If the student is banned from the course
        public bool IsBannedFromCourse { get; set; } = false;

        // If the student is suspended from the institution
        public bool IsSuspended { get; set; } = false;

        // Number of years a student is suspended
        public int YearsSuspended { get; set; } = 2;

        // Appeal information
        public DateTime? AppealDate { get; set; }

        [StringLength(2000)]
        public string? AppealDescription { get; set; }

        [StringLength(50)]
        public string? AppealStatus { get; set; } // Pending, Approved, Rejected

        [StringLength(2000)]
        public string? AppealDecision { get; set; }

        public DateTime? AppealDecisionDate { get; set; }

        // Resolution
        public DateTime? ResolvedDate { get; set; }

        [StringLength(2000)]
        public string? ResolutionNotes { get; set; }

        // Soft delete
        public DateTime? DeletedAt { get; set; }
    }
}

