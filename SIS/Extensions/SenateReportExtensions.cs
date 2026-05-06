using SIS.Models.Reports;

namespace SIS.Extensions
{
    public static class SenateReportExtensions
    {
        public static string GetProgressionRuleDisplayName(this string action)
        {
            return action switch
            {
                "Proceed" => "Clear Pass",
                "ProceedWithRepeat" => "Proceed with Repeat",
                "ProceedOnProbation" => "Proceed on Probation",
                "RepeatYear" => "Repeat Year",
                "RepeatSemester" => "Repeat Semester",
                "Exclude" => "Academic Exclusion",
                "Withdraw" => "Withdrawal",
                _ => action
            };
        }

        public static string GetProgressionRuleBootstrapClass(this string action)
        {
            return action switch
            {
                "Proceed" => "success",
                "ProceedWithRepeat" => "warning",
                "ProceedOnProbation" => "info",
                "RepeatYear" => "secondary",
                "RepeatSemester" => "secondary",
                "Exclude" => "danger",
                "Withdraw" => "dark",
                _ => "light"
            };
        }

        public static Dictionary<string, int> GetDefaultProgressionCounts()
        {
            return new Dictionary<string, int>
            {
                {"Proceed", 0},
                {"ProceedWithRepeat", 0},
                {"ProceedOnProbation", 0},
                {"RepeatYear", 0},
                {"RepeatSemester", 0},
                {"Exclude", 0},
                {"Withdraw", 0}
            };
        }

        public static string FormatPercentage(this decimal value, int decimals = 1)
        {
            return $"{Math.Round(value, decimals)}%";
        }

        public static string FormatGPA(this decimal value, int decimals = 2)
        {
            return Math.Round(value, decimals).ToString($"F{decimals}");
        }

        public static bool HasResults(this EntityProgressionSummary summary)
        {
            return summary.StudentsWithResults > 0;
        }

        public static int GetTotalProgression(this Dictionary<string, int> progressionCounts)
        {
            return progressionCounts.Values.Sum();
        }

        public static string GetReportLevelDisplayName(this string level)
        {
            return level switch
            {
                "School" => "Schools",
                "Department" => "Departments",
                "Programme" => "Programmes",
                _ => level
            };
        }

        public static string GenerateReportKey(this SenateReportFilters filters)
        {
            var keyComponents = new List<string>
            {
                $"AY:{filters.AcademicYearId ?? 0}",
                $"S:{filters.SchoolId ?? 0}",
                $"D:{filters.DepartmentId ?? 0}",
                $"P:{filters.ProgrammeId ?? 0}",
                $"M:{filters.ModeOfStudyId ?? 0}",
                $"Y:{filters.YearOfStudy ?? 0}",
                $"Sem:{filters.AcademicPeriod ?? 0}",
                $"L:{filters.ReportLevel}",
                $"Per:{filters.Period ?? "Current"}"
            };

            return string.Join("|", keyComponents);
        }

        public static bool IsValidForDrillDown(this SenateReportFilters filters, string targetLevel)
        {
            return targetLevel.ToLower() switch
            {
                "department" => filters.SchoolId.HasValue,
                "programme" => filters.DepartmentId.HasValue,
                _ => false
            };
        }

        public static SenateReportFilters CreateDrillDownFilters(this SenateReportFilters baseFilters,
            string newLevel, int entityId)
        {
            var newFilters = new SenateReportFilters
            {
                AcademicYearId = baseFilters.AcademicYearId,
                ModeOfStudyId = baseFilters.ModeOfStudyId,
                YearOfStudy = baseFilters.YearOfStudy,
                AcademicPeriod = baseFilters.AcademicPeriod,
                Period = baseFilters.Period,
                ReportLevel = newLevel
            };

            switch (newLevel.ToLower())
            {
                case "department":
                    newFilters.SchoolId = entityId;
                    break;
                case "programme":
                    newFilters.DepartmentId = entityId;
                    newFilters.SchoolId = baseFilters.SchoolId;
                    break;
            }

            return newFilters;
        }
    }
}