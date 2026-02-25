using SIS.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.StudentAccommodation
{
    public class MaintenanceRequest : AuditClass
    {
        [Key]
        public int RequestId { get; set; }

        [ForeignKey("Room")]
        public int RoomId { get; set; }
        public virtual Room Room { get; set; }

        // For requestedBy - it could be a student or staff
        public string RequestedBy { get; set; }  // This will store either StudentId or StaffId
        public string RequesterType { get; set; } // "Student" or "Staff"

        [Required]
        public DateTime RequestDate { get; set; }

        [Required]
        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        [StringLength(20)]
        public string Priority { get; set; } // low/medium/higha

        [Required]
        public Status Status { get; set; } // Using your existing Status enum

        public DateTime? ResolutionDate { get; set; }

        [StringLength(500)]
        public string ResolutionNotes { get; set; }


    }
}
