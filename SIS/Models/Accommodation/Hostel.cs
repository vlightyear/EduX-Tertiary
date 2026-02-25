using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using SIS.Enums;
using SIS.Data;
namespace SIS.Models.StudentAccommodation
{
    public class Hostel : AuditClass
    {
        [Key]
        public int HostelId { get; set; }

        [Required]
        public string HostelName { get; set; }

        [Required]
        public string Gender { get; set; } // male/female/mixed

        public int CampusId { get; set; }

        [ForeignKey("CampusId")]
        public virtual Campus Campus { get; set; }

        public string WardenId { get; set; }

        [ForeignKey("WardenId")]
        public ApplicationUser? Warden { get; set; }

        [Required]
        public int TotalRooms { get; set; }

        
        public int? TotalCapacity { get; set; }

        [Required]
        public Status Status { get; set; } // active/inactive

        public string Description { get; set; }

        // New properties for automatic room generation
        [Required]
        public string DefaultRoomType { get; set; } = "Single"; // Default room type for generated rooms

        [Required]
        public int DefaultCapacity { get; set; } = 1; // Default capacity for generated rooms

        public int RoomsPerFloor { get; set; } = 10; // Number of rooms per floor

        public string RoomNumberingPattern { get; set; } = "F{0}R{1}"; // Pattern for room numbering: F1R01, F1R02, etc.

        public bool AutoGenerateBeds { get; set; } = true; // Whether to automatically generate bed spaces

        public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();
    }
}