namespace SIS.Models
{
    public class Country
    {
        public string CountryName { get; set; }
    }

    public class State
    {
        public string Name { get; set; }
        public string StateCode { get; set; }
    }

    public class Data
    {
        public string Name { get; set; }
        public string Iso3 { get; set; }
        public string Iso2 { get; set; }
        public List<State> States { get; set; }
    }

    public class ApiResponse
    {
        public bool Error { get; set; }
        public string Msg { get; set; }
        public Data Data { get; set; }
    }
    public class CitiesResponse
    {
        public bool Error { get; set; }
        public string Msg { get; set; }
        public List<string> Data { get; set; }
    }

}
