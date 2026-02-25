using SIS.Data;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.StudentAccommodation
{
    public class CheckInOut
    {
        [Key]
        public int CheckId { get; set; }

        public int AllocationId { get; set; }

        [ForeignKey("AllocationId")]
        public virtual Allocation Allocation { get; set; }

        public DateTime? CheckInDate { get; set; }

        public string CheckInCondition { get; set; } = string.Empty;

        public string? CheckInStaffId { get; set; } 

        [ForeignKey("CheckInStaffId")]
        public virtual ApplicationUser CheckInStaff { get; set; }

        public DateTime? CheckOutDate { get; set; }

        public string CheckOutCondition { get; set; } = string.Empty;

        public string? CheckOutStaffId { get; set; }

        [ForeignKey("CheckOutStaffId")]
        public virtual ApplicationUser CheckOutStaff { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DamageCharges { get; set; } = 0;
    }
}
