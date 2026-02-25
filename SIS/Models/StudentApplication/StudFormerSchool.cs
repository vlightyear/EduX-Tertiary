using System.ComponentModel.DataAnnotations;

namespace SIS.Models.StudentApplication
{
    public class StudFormerSchool
    {
        [Key]
        public int Id { get; set; }
        // Previous School details
        public string? SchoolName { get; set; }
        public string? SchoolAddress { get; set; }
        public string? SchoolLevel { get; set; }
        public string? YearOfCompletion { get; set; }
        public string? SchoolResultsCopy { get; set; }
        // primary and secondary school
        public string? PrimarySchoolName { get; set; }
        public string? PrimarySchoolAddress { get; set; }
        public string? PrimarySchoolPeriod { get; set; }
        public string? SecondarySchoolName { get; set; }
        public string? SecondarySchoolAddress { get; set; }
        public string? SecondarySchoolPeriod { get; set; }
    }
}