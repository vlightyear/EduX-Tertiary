using SIS.Enums;
using SIS.Models.Admin;
using SIS.Models.Applications;
using SIS.Models.Registration;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.StudentApplication
{
    public class Applicant : AuditClass
    {
        [Key]
        public int ApplicantId { get; set; }

        public bool IsForeigner { get; set; }
        public string? ReferenceNumber { get; set; }

        // Personal details
        public required string FullName { get; set; }
        [DataType(DataType.Date)]
        public required DateTime DateOfBirth { get; set; }
        public required string Gender { get; set; }
        public required string Phone { get; set; }
        public required string Email { get; set; }
        public required string NrcOrPassport { get; set; }
        public required string MaritalStatus { get; set; }
        public required string Nationality { get; set; }
        public required string Religion { get; set; }

        // Address properties
        public required string AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public required string City { get; set; }
        public required string State { get; set; }
        public required string PostalCode { get; set; }
        public required string Country { get; set; }

        // Next of Kin details
        public required string NextOfKinName { get; set; }
        public required string NextOfKinRelation { get; set; }
        public required string NextOfKinPhone { get; set; }
        public required string NextOfKinEmail { get; set; }
        public required string NextOfKinAddress { get; set; }

        // Previous School details
        public string? FormerSchoolName { get; set; }
        public string? FormerSchoolAddress { get; set; }
        public required string FormerSchoolLevel { get; set; }
        public string? YearOfCompletion { get; set; }
        // Change from required to nullable
        public string? PrimarySchoolName { get; set; }
        public string? PrimarySchoolAddress { get; set; }
        public string? PrimarySchoolPeriod { get; set; }

        public required string SecondarySchoolName { get; set; }
        public required string SecondarySchoolAddress { get; set; }
        public required string SecondarySchoolPeriod { get; set; }

        // Navigation property for SubjectGrades
        public ICollection<ApplicantSubject>? SubjectGrades { get; set; } = new List<ApplicantSubject>();

        // paths for attachments
        public string? ResultsAttachmentCopy { get; set; }
        public string? NrcOrPassportCopy { get; set; }
        public string? StudyPermitCopy { get; set; }
        public string? PassportPhotoPath { get; set; }
        public bool? IsQualified { get; set; }

        // Foreign key properties
        public int SchoolId { get; set; }
        public School School { get; set; } = null!; // Navigation property
        public int ProgrammeId { get; set; }
        public Programme Programme { get; set; } = null!; // Navigation property

        public int ModeOfStudyId { get; set; }
        public ModeOfStudy ModeOfStudy { get; set; } = null!; // Navigation property

        public int AcademicYearId { get; set; }
        public AcademicYear AcademicYear { get; set; } = null!; // Navigation property
        //public ICollection<Payment> PaymentsDetails { get; set; } = new List<Payment>();

        public int? ApplicationPeriodId { get; set; }
        [ForeignKey(nameof(ApplicationPeriodId))]
        public ApplicationPeriod ApplicationPeriod { get; set; }


        // Indicates if the application is submitted
        public required int ProgrammeLevelId { get; set; }
        public ProgramLevel ProgrammeLevel { get; set; } = null!;

        public required bool IsSubmitted { get; set; }
        public required Status PaymentStatus { get; set; } // "Pending", "Completed", "Failed"
        public DateTime DateSubmitted { get; set; }
        public string? RejectReason { get; set; }
        public required Status Status { get; set; } // "Pending", "Accepted", "Rejected", "Waitlisted"

        // New Fields for Admission Process
        public Status AdmissionStatus { get; set; } // "Pending", "Accepted", "Rejected", "Waitlisted"
        public bool IsPlacedOnWaitingList { get; set; }
        public string AssistantRegistrarId { get; set; } // ID of the Assistant Registrar who is reviewing
    }
}
