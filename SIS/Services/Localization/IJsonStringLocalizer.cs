namespace SIS.Services.Localization
{
    public interface IJsonStringLocalizer
    {
        string this[string key] { get; }
        string CurrentCulture { get; }
        IReadOnlyList<LanguageOption> GetSupportedLanguages();
        bool IsSupportedCulture(string culture);
    }
}
