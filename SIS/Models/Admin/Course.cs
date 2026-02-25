using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Fees;
using SIS.Models.Lecturer;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Admin
{
    public class Course : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Course code is required.")]
        [StringLength(50)]
        [Display(Name = "Course Code")]
        public string CourseCode { get; set; }

        [Required(ErrorMessage = "Course name is required.")]
        [StringLength(100)]
        [Display(Name = "Course Name")]
        public string CourseName { get; set; }

        [Required(ErrorMessage = "Course type is required.")]
        [Display(Name = "Course Type")]
        public string CourseType { get; set; } // "Full Course" or "Half Course"

        [Required(ErrorMessage = "Course description is required.")]
        [StringLength(500)]
        [Display(Name = "Course Description")]
        public string CourseDescription { get; set; }

        // Academic Details
        [Required(ErrorMessage = "Programme is required.")]
        [Display(Name = "Programme")]
        public int ProgrammeID { get; set; }

        [ForeignKey("ProgrammeID")]
        [DeleteBehavior(DeleteBehavior.NoAction)]
        public virtual Programme Programme { get; set; }

        [Required(ErrorMessage = "Pass mark is required.")]
        [Range(0, 100, ErrorMessage = "Pass mark must be between 0 and 100.")]
        [Display(Name = "Pass Mark")]
        public double PassMark { get; set; }

        [Required(ErrorMessage = "Year taken is required.")]
        [Display(Name = "Year Taken")]
        [Range(1, 6, ErrorMessage = "Year must be between 1 and 6.")]
        public int YearTaken { get; set; }

        [Required(ErrorMessage = "Semester is required.")]
        [Display(Name = "Semester")]
        [Range(1, 2, ErrorMessage = "Semester must be 1 or 2.")]
        public int SemesterTaken { get; set; }

        // Course Settings
        [Required]
        [Display(Name = "Is Mandatory")]
        public bool IsMandatory { get; set; }

        [Required]
        [Display(Name = "Is Examinable")]
        public bool IsExaminable { get; set; } = true;


        // New fields
        [Required(ErrorMessage = "Instructor is required.")]
        public string? InstructorId { get; set; }


        [Required(ErrorMessage = "Meeting frequency per week is required.")]
        [Range(1, 7)]
        [Display(Name = "Meetings Per Week")]
        public int MeetingFrequencyPerWeek { get; set; }

        [Required(ErrorMessage = "Capacity is required.")]
        [Display(Name = "Capacity Required")]
        public int CapacityRequired { get; set; }

        [Display(Name = "Preferred Venues")]
        [Column(TypeName = "nvarchar(max)")]
        public string? PreferredVenueIds { get; set; } // JSON array of venue IDs
                                                       // Add this to your Course class
        [Display(Name = "Prerequisite Courses")]
        [Column(TypeName = "nvarchar(max)")]
        public string? PrerequisiteCourseIds { get; set; } // JSON array of prerequisite course IDs

        [NotMapped]
        public int Credits { get; set; } = 3;

        // Navigation properties using join tables
        public virtual ICollection<CourseLecturer> CourseLecturers { get; set; } = new List<CourseLecturer>();
        public virtual ICollection<CoursePrerequisite> Prerequisites { get; set; } = new List<CoursePrerequisite>();
        public virtual ICollection<CourseAssessment> CourseAssessments { get; set; } = new List<CourseAssessment>();
        public virtual ICollection<ChapterProgress> ChapterProgresses { get; set; }
    }
}
