using SIS.Enums;
using SIS.Models.Registration;
using SIS.Models.Admin;
using SIS.Models.Fees;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SIS.Models.StudentAccommodation
{
    public class AccommodationPeriod : AuditClass
    {
        [Key]
        public int PeriodId { get; set; }


        [Required]
        public DateTime StartDate { get; set; }

        
        public DateTime? EndDate { get; set; }

        public string? TypeOfPayment { get; set; }//Semister, Year, PerDay, Fixed
        [Precision(18,2)]
        public decimal TypeOfPaymentAmount {  get; set; }


        public string? Type { get; set; } // semester/year/custom

        [Required]
        public DateTime ApplicationStartDate { get; set; }

        [Required]
        public DateTime ApplicationEndDate { get; set; }

        [Required]
        public Status Status { get; set; } // upcoming/active/closed

        // New fields from FeeConfiguration - all made nullable
        [ForeignKey("School")]
        public int? SchoolId { get; set; }

        [ForeignKey("Programme")]
        public int? ProgrammeId { get; set; }

        [ForeignKey("ModeOfStudy")]
        public int? ModeOfStudyId { get; set; }

        public int? YearOfStudy { get; set; }

        [ForeignKey("ProgramLevel")]
        public int? ProgramLevelId { get; set; }

        // New field for handling long-term accommodation
        public bool IsPermanentUntilGraduation { get; set; }

        // New field for universal application
        public bool AppliesUniversally { get; set; }

        // Navigation properties
        public virtual ICollection<AccommodationApplication> Applications { get; set; } = new List<AccommodationApplication>();

        // New navigation properties
        public virtual School School { get; set; }
        public virtual Programme Programme { get; set; }
        public virtual ModeOfStudy ModeOfStudy { get; set; }
        public virtual ProgramLevel ProgramLevel { get; set; }
      
    }
}