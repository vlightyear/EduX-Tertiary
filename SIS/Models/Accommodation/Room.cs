using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using SIS.Enums;

namespace SIS.Models.StudentAccommodation
{
    public class Room : AuditClass
    {
        [Key]
        public int RoomId { get; set; }

        [Required]
        public int HostelId { get; set; }

        [ForeignKey("HostelId")]
        public virtual Hostel Hostel { get; set; }

        [Required]
        public string RoomNumber { get; set; }

        public int Floor { get; set; }

        [Required]
        public string RoomType { get; set; } // single/double/triple/suite

        [Required]
        public int Capacity { get; set; }

        [Required]
        public string Gender { get; set; } // male/female/any

        [Required]
        public Status Status { get; set; }

        public bool IsSpecialReservation { get; set; } = false;

        public virtual ICollection<BedSpace> BedSpaces { get; set; } = new List<BedSpace>();

        public virtual ICollection<RoomResource> Resources { get; set; } = new List<RoomResource>();

        public virtual ICollection<MaintenanceRequest> MaintenanceRequests { get; set; } = new List<MaintenanceRequest>();
    }
}
