using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Admin
{
    public class LearningRoom
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Display(Name = "Room Name")]
        public string Name { get; set; }

        [Required]
        public int BuildingId { get; set; }

        [ForeignKey("BuildingId")]
        public Building? Building { get; set; }

        [MaxLength(250)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        // Optional: Type of room (e.g., Classroom, Laboratory, Computer Lab, etc.)
        [Display(Name = "Room Type")]
        public string RoomType { get; set; }

        // Learning capacity
        [Required]
        [Display(Name = "Learning Capacity")]
        public int LearningCapacity { get; set; }

        // Exam capacity
        [Required]
        [Display(Name = "Exam Capacity")]
        public int ExamCapacity { get; set; }

        // Area of the room (not required)
        [Display(Name = "Room Area (sq ft)")]
        public double? Area { get; set; } // Nullable for optional field

        [Required]
        [Display(Name = "Created Date")]
        [DataType(DataType.Date)]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Required]
        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;
    }
}