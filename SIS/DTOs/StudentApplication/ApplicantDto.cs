using SIS.Models.StudentApplication;
using System.ComponentModel.DataAnnotations;

namespace SIS.DTOs.StudentApplication
{
    public class ApplicantDto
    {
        public int ApplicantId { get; set; }
        public string NRCOrPassport { get; set; }
        public string FullName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string MaritalStatus { get; set; }
        public string Nationality { get; set; }
        public string Religion { get; set; }
        

        // Address details
        public string AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }

        // Next of Kin details
        public string NextOfKinName { get; set; }
        public string NextOfKinRelation { get; set; }
        public string NextOfKinPhone { get; set; }
        public string NextOfKinEmail { get; set; }
        public string NextOfKinAddress { get; set; }


        public string FormerSchoolName { get; set; }
        public string FormerSchoolAddress { get; set; }
        public string YearOfCompletion { get; set; }
        public string FormerSchoolLevel { get; set; }

       
        public string? PrimarySchoolName { get; set; }
        public string? PrimarySchoolAddress { get; set; }
        public string? PrimarySchoolPeriod { get; set; }

        public string SecondarySchoolName { get; set; }
        public string SecondarySchoolAddress { get; set; }
        public string SecondarySchoolPeriod { get; set; }


        public IFormFile ResultsAttachment { get; set; }
        public IFormFile NrcOrPassportCopy { get; set; }
        public IFormFile StudyPermit { get; set; }

        public IFormFile PassportPhoto { get; set; }


        // Other properties
        public List<SubjectGradeDto> SelectedSubjects { get; set; }


        public int SchoolId { get; set; }
        public int ProgrammeId { get; set; }
        public int ModeOfStudyId { get; set; }
        public int AcademicYearId { get; set; }
        public int ProgrammeLevel { get; set; }

        // Indicates if the application is submitted
        public bool IsSubmitted { get; set; } = false;
        // Additional field
        public bool IsForeigner { get; set; }

    }



    public class InitApplicationDto
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string ReferenceNumber { get; set; }
    }
}
