namespace SIS.DTOs
{
    public class ChapterUpdateRequest
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int OrderIndex { get; set; }
        public bool IsActive { get; set; }
    }
}
