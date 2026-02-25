using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Identity
{
    public class Permission
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } // e.g., "Students.View", "Invoices.Create"

        [StringLength(200)]
        public string Description { get; set; }

        [StringLength(50)]
        public string Category { get; set; } // e.g., "Students", "Finance", "Academic"

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }

    public class RolePermission
    {
        public int Id { get; set; }

        [Required]
        public string RoleId { get; set; }

        public int PermissionId { get; set; }

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

        public string GrantedBy { get; set; }

        // Navigation properties
        public virtual IdentityRole Role { get; set; }
        public virtual Permission Permission { get; set; }
    }
}
