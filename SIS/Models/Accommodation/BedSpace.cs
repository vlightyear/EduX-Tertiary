using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using SIS.Enums;
using SIS.Data;
using SIS.Models.StudentApplication;

namespace SIS.Models.StudentAccommodation
{
    public class BedSpace : AuditClass
    {
        [Key]
        public int BedId { get; set; }

        [Required]
        public int RoomId { get; set; }

        [ForeignKey("RoomId")]
        public virtual Room Room { get; set; }

        [Required]
        public string BedIdentifier { get; set; } // A/B/1/2
        public bool IsSpecialReservation { get; set; } = false;
        public int CurrentStudentYear { get; set; } = 0; // for all years upto 7
        public int CurrentStudentSemister { get; set; } = 0; // for all semisters, upto 2

        [Required]
        public Status Status { get; set; }
    }
}
