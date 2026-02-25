namespace SIS.Models.Lecturer
{
    // Updated view models for CourseStudents with renamed classes
    public class CourseStudentsViewModel
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public int TotalStudents { get; set; }
        public int TotalChapters { get; set; }
        public List<CourseStudentGroupModel> StudentGroups { get; set; }
        public List<CourseChapterModel> Chapters { get; set; }
    }

    public class CourseStudentGroupModel
    {
        public int AcademicYearId { get; set; }
        public int SemesterId { get; set; }
        public string AcademicYear { get; set; }
        public string Semester { get; set; }
        public List<CourseStudentProgressModel> Students { get; set; }
    }

    public class CourseStudentProgressModel
    {
        public string StudentId { get; set; }
        public string StudentNumber { get; set; }
        public string FirstName { get; set; } // Will contain full name
        public string LastName { get; set; } // Will be empty
        public string Email { get; set; }
        public DateTime EnrollmentDate { get; set; }
        public string ModeOfStudy { get; set; } // Added to display mode of study
        public List<CourseChapterProgressModel> ChapterProgress { get; set; }
        public int OverallProgress { get; set; }

        // Convenience property - will just return FirstName since it contains the full name
        public string FullName => FirstName;
    }

    public class CourseChapterProgressModel
    {
        public int ChapterId { get; set; }
        public string ChapterTitle { get; set; }
        public bool IsCompleted { get; set; }
        public int ProgressPercentage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedDate { get; set; }
    }

    public class CourseChapterModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int OrderIndex { get; set; }
    }
}
