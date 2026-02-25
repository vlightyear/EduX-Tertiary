using SIS.Models.Admin;

namespace SIS.Models.Registration
{
    public class AcademicYearViewModel
    {
        public IEnumerable<AcademicYear> AcademicYears { get; set; }
        public IEnumerable<WorkingDayConfiguration> WorkingDayConfigs { get; set; }
    }
}
