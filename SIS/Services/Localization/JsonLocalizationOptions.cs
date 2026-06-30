namespace SIS.Services.Localization
{
    public sealed class JsonLocalizationOptions
    {
        public const string SectionName = "JsonLocalization";

        public string ResourcesPath { get; set; } = "Localization";
        public string DefaultCulture { get; set; } = "en";
        public string CookieName { get; set; } = "EduX.Culture";
    }
}
