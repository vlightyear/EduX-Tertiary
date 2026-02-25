using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using SIS.Enums;

namespace SIS.Models.StudentAccommodation
{
    public class RoomResource : AuditClass
    {
        [Key]
        public int ResourceId { get; set; }

        [Required]
        public int RoomId { get; set; }

        [ForeignKey("RoomId")]
        public virtual Room Room { get; set; }
        [Required]
        public int ResourceTypeId { get; set; }

        [ForeignKey("ResourceTypeId")]  
        public virtual ResourceType ResourceType { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        public Status Status { get; set; }
    }
}
