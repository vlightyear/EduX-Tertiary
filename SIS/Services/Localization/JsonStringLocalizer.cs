using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SIS.Services.Localization
{
    public sealed class JsonStringLocalizer : IJsonStringLocalizer
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly JsonLocalizationOptions _options;
        private readonly ConcurrentDictionary<string, CachedLanguage> _cache = new(StringComparer.OrdinalIgnoreCase);

        public JsonStringLocalizer(
            IWebHostEnvironment environment,
            IHttpContextAccessor httpContextAccessor,
            IOptions<JsonLocalizationOptions> options)
        {
            _environment = environment;
            _httpContextAccessor = httpContextAccessor;
            _options = options.Value;
        }

        public string this[string key]
        {
            get
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    return string.Empty;
                }

                var culture = CurrentCulture;
                if (TryGetValue(culture, key, out var value))
                {
                    return value;
                }

                if (!culture.Equals(_options.DefaultCulture, StringComparison.OrdinalIgnoreCase) &&
                    TryGetValue(_options.DefaultCulture, key, out value))
                {
                    return value;
                }

                return key;
            }
        }

        public string CurrentCulture
        {
            get
            {
                var culture = _httpContextAccessor.HttpContext?.Items[JsonLocalizationMiddleware.CultureItemKey]?.ToString();
                return string.IsNullOrWhiteSpace(culture) ? _options.DefaultCulture : culture;
            }
        }

        public IReadOnlyList<LanguageOption> GetSupportedLanguages()
        {
            var directory = GetLocalizationDirectory();
            if (!Directory.Exists(directory))
            {
                return new[] { new LanguageOption(_options.DefaultCulture, _options.DefaultCulture.ToUpperInvariant()) };
            }

            return Directory
                .EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                .Select(path =>
                {
                    var culture = Path.GetFileNameWithoutExtension(path);
                    var name = TryGetValue(culture, "Language.DisplayName", out var displayName)
                        ? displayName
                        : culture.ToUpperInvariant();

                    return new LanguageOption(culture, name);
                })
                .OrderBy(language => language.DisplayName)
                .ToArray();
        }

        public bool IsSupportedCulture(string culture)
        {
            if (string.IsNullOrWhiteSpace(culture))
            {
                return false;
            }

            var filePath = GetLanguageFilePath(culture);
            return File.Exists(filePath);
        }

        private bool TryGetValue(string culture, string key, out string value)
        {
            var language = GetLanguage(culture);
            return language.Values.TryGetValue(key, out value!);
        }

        private CachedLanguage GetLanguage(string culture)
        {
            var filePath = GetLanguageFilePath(culture);
            var lastWriteUtc = File.Exists(filePath)
                ? File.GetLastWriteTimeUtc(filePath)
                : DateTime.MinValue;

            if (_cache.TryGetValue(culture, out var cached) && cached.LastWriteUtc == lastWriteUtc)
            {
                return cached;
            }

            var values = LoadLanguageFile(filePath);
            var updated = new CachedLanguage(lastWriteUtc, values);
            _cache.AddOrUpdate(culture, updated, (_, _) => updated);
            return updated;
        }

        private Dictionary<string, string> LoadLanguageFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            using var stream = File.OpenRead(filePath);
            using var document = JsonDocument.Parse(stream, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            FlattenElement(document.RootElement, null, values);
            return values;
        }

        private static void FlattenElement(JsonElement element, string? prefix, Dictionary<string, string> values)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    var key = string.IsNullOrWhiteSpace(prefix) ? property.Name : $"{prefix}.{property.Name}";
                    FlattenElement(property.Value, key, values);
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(prefix))
            {
                values[prefix] = element.ValueKind == JsonValueKind.String
                    ? element.GetString() ?? string.Empty
                    : element.ToString();
            }
        }

        private string GetLanguageFilePath(string culture)
        {
            var safeCulture = Path.GetFileNameWithoutExtension(culture);
            return Path.Combine(GetLocalizationDirectory(), $"{safeCulture}.json");
        }

        private string GetLocalizationDirectory()
        {
            return Path.Combine(_environment.ContentRootPath, _options.ResourcesPath);
        }

        private sealed record CachedLanguage(DateTime LastWriteUtc, Dictionary<string, string> Values);
    }
}
