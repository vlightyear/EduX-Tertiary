using SIS.Models.Registration;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.StudentApplication
{
    public class StudentGceSubjects
    {
        public required int SubjectId { get; set; } // Foreign Key to Subject
        public Subject Subject { get; set; }

        public required int GradeId { get; set; } // Foreign Key to Grade
        public Grade Grade { get; set; }
    }
}
