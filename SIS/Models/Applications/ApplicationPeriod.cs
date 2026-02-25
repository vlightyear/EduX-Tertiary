using SIS.Models.Admin;
using SIS.Models.Registration;
using SIS.Models.StudentApplication;

namespace SIS.Models.Applications
{
    public class ApplicationPeriod
    {
        public int Id {  get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime StartOfApplication { get; set; }
        public DateTime EndOfApplication { get; set; }
        public int Year {  get; set; }
        public List<ModeOfStudy> ModeOfStudies {  get; set; }
        public List<Programme> Programms { get; set; }
        public List<Applicant> Applicants { get; set; }
    }
}
