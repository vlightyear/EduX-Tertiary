namespace SIS.Models.Admin
{
    public class CourseFilterRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string SearchTerm { get; set; } = "";
        public string CourseType { get; set; } = "all";
        public string Year { get; set; } = "all";
        public string Programme { get; set; } = "all";
        public string SortColumn { get; set; } = "index";
        public string SortDirection { get; set; } = "asc";
    }

    public class CourseListResponse
    {
        public List<CourseViewModel> Courses { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
    }

    public class CourseViewModel
    {
        public int Id { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public string CourseType { get; set; }
        public int YearTaken { get; set; }
        public int SemesterTaken { get; set; }
        public bool IsMandatory { get; set; }
        public string ProgrammeName { get; set; }
        public string SchoolName { get; set; }
        public int RowNumber { get; set; }
    }

    public class CourseStatistics
    {
        public int TotalCourses { get; set; }
        public int FullCourses { get; set; }
        public int HalfCourses { get; set; }
        public int MandatoryCourses { get; set; }
        public List<CourseTypeDistribution> CourseDistribution { get; set; }
    }

    public class CourseTypeDistribution
    {
        public string Type { get; set; }
        public int Count { get; set; }
    }
}
