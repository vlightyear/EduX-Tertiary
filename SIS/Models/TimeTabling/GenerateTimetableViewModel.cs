using System.ComponentModel.DataAnnotations;

namespace SIS.Models.TimeTabling
{
    public class GenerateTimetableViewModel
    {
        [Required(ErrorMessage = "Academic Year is required")]
        [Display(Name = "Academic Year")]
        public int AcademicYearId { get; set; }

        [Required(ErrorMessage = "Mode of Study is required")]
        [Display(Name = "Mode of Study")]
        public int ModeOfStudyId { get; set; }
    }
}