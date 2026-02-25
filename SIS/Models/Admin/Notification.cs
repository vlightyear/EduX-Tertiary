using SIS.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models
{
    public class Notification : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public string Message { get; set; }

        public string? Link { get; set; }

        public NotificationType Type { get; set; } // Requires NotificationType enum


        public DateTime ExpiryDate { get; set; }

        public bool IsActive { get; set; } = true;

        // Optional: Target specific users or roles
        public string? TargetUserEmail { get; set; } // If null, visible to all
        public string? TargetRole { get; set; } // If null, visible to all roles
    }
}