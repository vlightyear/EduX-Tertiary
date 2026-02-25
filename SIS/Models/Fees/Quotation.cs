using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SIS.Models.Admin;
using SIS.Models.Registration;

namespace SIS.Models.Fees
{
    /// <summary>
    /// Represents a quotation for fees
    /// </summary>
    public class Quotation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string QuotationReference { get; set; }

        [StringLength(50)]
        public string BatchReference { get; set; }

        [Required]
        [StringLength(50)]
        public string StudentId { get; set; }

        /// <summary>
        /// Stores student name for data persistence (in case student record is deleted or modified)
        /// </summary>
        [StringLength(200)]
        public string StudentName { get; set; }

        [Required]
        public int AcademicYearId { get; set; }

        public int? Semester { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        public QuotationStatus Status { get; set; } = QuotationStatus.Pending;

        [StringLength(500)]
        public string QuotationDescription { get; set; }

        [Required]
        public DateTime ValidUntil { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; }

        [StringLength(100)]
        public string CreatedBy { get; set; }

        public DateTime? UpdatedDate { get; set; }

        [StringLength(100)]
        public string UpdatedBy { get; set; }

        public bool IsDeleted { get; set; } = false;

        // Navigation Properties
        // Note: Student navigation removed - use StudentId string directly
        // public virtual Student? Student { get; set; }

        public virtual AcademicYear? AcademicYear { get; set; }

        public virtual ICollection<QuotationItem> Items { get; set; } = new List<QuotationItem>();
    }

    /// <summary>
    /// Represents individual line items in a quotation
    /// </summary>
    public class QuotationItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int QuotationId { get; set; }

        [Required]
        public int FeeTypeId { get; set; }

        [Required]
        [StringLength(200)]
        public string FeeTypeName { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; }

        // Navigation Properties
        public virtual Quotation? Quotation { get; set; }

        public virtual FeeType? FeeType { get; set; }
    }

    /// <summary>
    /// Status of quotation
    /// </summary>
    public enum QuotationStatus
    {
        Pending = 0,
        Accepted = 1,
        Rejected = 2,
        Converted = 3,
        Expired = 4
    }
}