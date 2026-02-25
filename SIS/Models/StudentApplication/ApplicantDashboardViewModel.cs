using SIS.Enums;
using SIS.Models.Admin;
using SIS.Models.Registration;

namespace SIS.Models.StudentApplication
{
    public class ApplicantDashboardViewModel
    {
        public IEnumerable<Applicant> Applications { get; set; } = new List<Applicant>();
        public IEnumerable<School> Schools { get; set; } = new List<School>();
        public AcademicYear? CurrentAcademicYear { get; set; }

        public IEnumerable<Notification> Notifications { get; set; } = new List<Notification>();

        public Dictionary<Status, int> ApplicationStatistics { get; set; } = new Dictionary<Status, int>();
        public IEnumerable<Applicant> RecentApplications { get; set; } = new List<Applicant>();
        public bool HasIncompleteApplications { get; set; }
        public int IncompleteApplicationsCount { get; set; }
        public List<SchoolProgramCount> SchoolProgramData { get; set; } = new List<SchoolProgramCount>();
    }

    public class SchoolProgramCount
    {
        public string SchoolName { get; set; } = string.Empty;
        public int ProgramCount { get; set; }
    }
}
