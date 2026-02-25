using System.ComponentModel.DataAnnotations;

namespace SIS.Models.StudentAccommodation
{
    public class ResourceType : AuditClass
    {
        [Key]
        public int ResourceTypeId { get; set; }

        [Required]
        public string Name { get; set; } // chair/table/bulb/fan

        public string Description { get; set; }

        public virtual ICollection<RoomResource> RoomResources { get; set; } = new List<RoomResource>();
    }
}
