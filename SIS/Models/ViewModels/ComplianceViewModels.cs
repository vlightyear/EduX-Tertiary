namespace SIS.Models.ViewModels
{
    public class DocketLookupIndexViewModel
    {
        public string UserRole { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string JurisdictionInfo { get; set; } = string.Empty;
        public List<string> SearchTypes { get; set; } = new();

    }
}
