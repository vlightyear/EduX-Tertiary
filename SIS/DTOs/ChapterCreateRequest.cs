namespace SIS.DTOs
{
    public class ChapterCreateRequest
    {
        public int CourseId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int OrderIndex { get; set; }
    }
}
