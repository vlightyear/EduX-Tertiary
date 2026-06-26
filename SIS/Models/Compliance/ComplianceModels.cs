using AngleSharp.Dom;
using DocumentFormat.OpenXml.Spreadsheet;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Compliance
{
    [Table("vwStudentCourses")]
    public class StudentData
    {
        [Key]
        [Column("studentid_number")]
        public string StudentIdNumber { get; set; }

        [Column("studentId")]
        public int StudentId { get; set; }

        [Column("registrationStatus")]
        public bool RegistrationStatus { get; set; }

        [Column("academicyearid")]
        public int AcademicYearId { get; set; }

        [Column("academicyear")]
        public string AcademicYear { get; set; }

        [Column("schoolid")]
        public int SchoolId { get; set; }

        [Column("school")]
        public string School { get; set; }

        [Column("fullname")]
        public string FullName { get; set; }

        [Column("StudentCurrentyear")]
        public int StudentCurrentYear { get; set; }

        [Column("programmeId")]
        public int ProgrammeId { get; set; }

        [Column("programme")]
        public string Programme { get; set; }

        [Column("semesterofstudy")]
        public int CurrentYearPeriodId { get; set; }

        [NotMapped]
        public string CurrentPeriodLabel { get; set; } = string.Empty;

        [Column("Delivery")]
        public string Delivery { get; set; }

        [Column("NRCNo")]
        public string NRCNo { get; set; }

        [Column("PermitValid")]
        public int PermitValid { get; set; }

        [Column("OutstandingFees")]
        public decimal? OutstandingFees { get; set; }

        [Column("AmountPaid")]
        public decimal? AmountPaid { get; set; }

        [Column("InvoicedAmount")]
        public decimal? InvoicedAmount { get; set; }

        [Column("PercentPaid")]
        public decimal? PercentPaid { get; set; }

        [Column("courses_registered")]
        public string? CoursesRegistered { get; set; }
        [NotMapped]
        public string? DocketType { get; set; }
    }
}
