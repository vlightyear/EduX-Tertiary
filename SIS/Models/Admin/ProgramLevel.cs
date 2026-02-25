using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Admin
{
    public class ProgramLevel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } // e.g., "Masters", "Bachelors", "Diploma"

        [StringLength(200)]
        public string Description { get; set; } // Optional field for additional details about the level

        [Required]
        public int Rank { get; set; } // Used to order levels hierarchically, e.g., 1 for Diploma, 2 for Bachelors, 3 for Masters

        [Required]
        public bool IsActive { get; set; } // Indicates if the level is currently in use
    }
}
