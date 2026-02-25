using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Courses
{
    public class ChapterRatingRequest
    {
        [Required]
        public int ChapterId { get; set; }

        [Required]
        public int CourseId { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Please provide a rating between 1 and 5 stars")]
        public int Rating { get; set; }

        [StringLength(500, ErrorMessage = "Review cannot exceed 500 characters")]
        public string ReviewText { get; set; }
    }

    public class ChapterRatingViewModel
    {
        public int ChapterId { get; set; }
        public int CourseId { get; set; }
        public int? Rating { get; set; }
        public string ReviewText { get; set; }
        public bool HasRated { get; set; }
        public DateTime? RatedAt { get; set; }
    }

    public class ChapterRatingStatsViewModel
    {
        public int ChapterId { get; set; }
        public double AverageRating { get; set; }
        public int TotalRatings { get; set; }
        public Dictionary<int, int> RatingDistribution { get; set; } // Star -> Count
    }
}
