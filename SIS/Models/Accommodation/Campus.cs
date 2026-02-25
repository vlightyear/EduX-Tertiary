using Microsoft.Extensions.Hosting;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.StudentAccommodation
{

    public class Campus : AuditClass
    {
        [Key]
        public int CampusId { get; set; }

        [Required]
        public string CampusName { get; set; }

        public string Location { get; set; }

        public string Description { get; set; }
        public bool IsActive { get; set; } = true;

        public virtual ICollection<Hostel> Hostels { get; set; } = new List<Hostel>();
    }
}
