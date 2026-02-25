using SIS.DTOs.StudentApplication;
using SIS.Models.Admin;

namespace SIS.Models.StudentApplication
{
    public class PersonalDetailsViewModel
    {
        public ApplicantDto Applicant { get; set; }
        public List<SubjectGradeDto> SelectedSubjects { get; set; }
        public IEnumerable<DropdownOptionDto> Subjects { get; set; }
        public IEnumerable<DropdownOptionDto> Grades { get; set; }
        public IEnumerable<DropdownOptionDto> Schools { get; set; }
        public IEnumerable<DropdownOptionDto> Programmes { get; set; }
        public IEnumerable<DropdownOptionDto> ModesOfStudy { get; set; }
        public IEnumerable<DropdownOptionDto> AcademicYears { get; set; }
        public List<string> Countries { get; set; }
        public IEnumerable<DropdownOptionDto> ProgrammeLevel { get; set; }
    }


    public class ApplicantViewModel
    {
        public List<Applicant> Applications { get; set; }
        public List<School> Schools { get; set; }
    }
}
