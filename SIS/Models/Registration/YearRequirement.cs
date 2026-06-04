using System.Text.Json.Serialization;

namespace SIS.Models.Registration
{
    public class YearRequirement
    {
        public int TotalRequired { get; set; }

        // Canonical period properties (Period1/Period2/Period3 for Semester and Term)
        public int? Period1 { get; set; }
        public int? Period2 { get; set; }
        public int? Period3 { get; set; }

        // Backward-compat: legacy JSON stored Semester1/Semester2
        public int? Semester1 { get; set; }
        public int? Semester2 { get; set; }

        // Helpers that fall back from new names to old names
        [JsonIgnore] public int? GetPeriod1 => Period1 ?? Semester1;
        [JsonIgnore] public int? GetPeriod2 => Period2 ?? Semester2;
    }
}
