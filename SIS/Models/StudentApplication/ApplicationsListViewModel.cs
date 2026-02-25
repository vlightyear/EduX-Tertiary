using Microsoft.AspNetCore.Mvc.Rendering;

namespace SIS.Models.StudentApplication
{
    public class ApplicationsListViewModel
    {
        public List<Applicant> Applications { get; set; } = new List<Applicant>();
        public string SearchTerm { get; set; } = "";
        public string SelectedStatus { get; set; } = "";
        public string SelectedSchool { get; set; } = "";
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public List<SelectListItem> StatusOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> SchoolOptions { get; set; } = new List<SelectListItem>();

        public bool HasNextPage => CurrentPage < TotalPages;
        public bool HasPreviousPage => CurrentPage > 1;
    }
}
