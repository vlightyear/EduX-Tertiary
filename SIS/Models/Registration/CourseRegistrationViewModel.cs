namespace SIS.Models.Registration
{
    public class CourseRegistrationViewModel
    {
        public int Id { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public string CourseDescription { get; set; }
        public bool IsMandatory { get; set; }
        public bool IsSelected { get; set; }

        public bool IsCarryover { get; set; } = false;
        public string? CarryoverReason { get; set; }
        public bool CanRegister { get; set; } = false;
    }
}
