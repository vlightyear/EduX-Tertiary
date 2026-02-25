using SIS.Data;
using SIS.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.StudentAccommodation
{
    public class Allocation : AuditClass
    {
        [Key]
        public int AllocationId { get; set; }

        public int ApplicationId { get; set; }

        [ForeignKey("ApplicationId")]
        public virtual AccommodationApplication Application { get; set; }

        public int BedId { get; set; }

        [ForeignKey("BedId")]
        public virtual BedSpace Bed { get; set; }

        [Required]
        public string AllocationType { get; set; } // individual/bulk/special

        public string? AllocatedById { get; set; }

        [ForeignKey("AllocatedById")]
        public virtual ApplicationUser AllocatedBy { get; set; }

        [Required]
        public DateTime AllocationDate { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; } // null for "until graduation"

        public bool IsGraduationBased { get; set; } = false;

        [Required]
        public Status Status { get; set; } // AllocatedPending/AllocatedActive/AllocatedCompleted/AllocatedCancelled

        // Navigation properties
        public virtual CheckInOut CheckInOut { get; set; }
    }
}
