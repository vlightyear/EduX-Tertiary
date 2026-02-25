namespace SIS.Models.Lecturer
{
    public class CourseRatingAnalyticsViewModel
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public int TotalStudents { get; set; }
        public double OverallAverageRating { get; set; }
        public int TotalRatings { get; set; }
        public int TotalStudentsRated { get; set; }
        public List<StudentRatingGroup> StudentGroups { get; set; } = new List<StudentRatingGroup>();
        public List<ChapterRatingStats> ChapterStats { get; set; } = new List<ChapterRatingStats>();
        public Dictionary<int, int> RatingDistribution { get; set; } = new Dictionary<int, int>();
        public List<Chapter> Chapters { get; set; } = new List<Chapter>();
    }

    public class StudentRatingGroup
    {
        public int AcademicYearId { get; set; }
        public string AcademicYear { get; set; }
        public int SemesterId { get; set; }
        public string Semester { get; set; }
        public string ModeOfStudy { get; set; }
        public List<MaskedStudentRating> Students { get; set; } = new List<MaskedStudentRating>();
    }

    public class MaskedStudentRating
    {
        public string MaskedStudentNumber { get; set; }
        public string StudentHashId { get; set; } // For expandable details
        public DateTime EnrollmentDate { get; set; }
        public double AverageRating { get; set; }
        public int ChaptersRated { get; set; }
        public int TotalChapters { get; set; }
        public decimal RatingCompletionPercentage { get; set; }
        public List<ChapterRatingDetail> ChapterRatings { get; set; } = new List<ChapterRatingDetail>();
        public DateTime? LastRatingDate { get; set; }
        public string ModeOfStudy { get; set; }
    }

    public class ChapterRatingDetail
    {
        public int ChapterId { get; set; }
        public string ChapterTitle { get; set; }
        public int OrderIndex { get; set; }
        public int? Rating { get; set; }
        public string ReviewText { get; set; }
        public DateTime? RatedDate { get; set; }
        public bool HasRating { get; set; }
    }

    public class ChapterRatingStats
    {
        public int ChapterId { get; set; }
        public string ChapterTitle { get; set; }
        public int OrderIndex { get; set; }
        public double AverageRating { get; set; }
        public int TotalRatings { get; set; }
        public Dictionary<int, int> RatingDistribution { get; set; } = new Dictionary<int, int>();
        public List<AnonymousReview> RecentReviews { get; set; } = new List<AnonymousReview>();
        public double CompletionRate { get; set; } // Percentage of students who rated this chapter
    }

    public class AnonymousReview
    {
        public string ReviewText { get; set; }
        public int Rating { get; set; }
        public DateTime ReviewDate { get; set; }
        public string AnonymousIdentifier { get; set; } // "Anonymous Student A", etc.
    }

}
