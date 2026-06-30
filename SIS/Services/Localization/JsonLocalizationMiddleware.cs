using Microsoft.Extensions.Options;

namespace SIS.Services.Localization
{
    public sealed class JsonLocalizationMiddleware
    {
        public const string CultureItemKey = "JsonLocalization.Culture";

        private readonly RequestDelegate _next;
        private readonly JsonLocalizationOptions _options;

        public JsonLocalizationMiddleware(RequestDelegate next, IOptions<JsonLocalizationOptions> options)
        {
            _next = next;
            _options = options.Value;
        }

        public async Task InvokeAsync(HttpContext context, IJsonStringLocalizer localizer)
        {
            var culture = ResolveCulture(context, localizer);
            context.Items[CultureItemKey] = culture;

            await _next(context);
        }

        private string ResolveCulture(HttpContext context, IJsonStringLocalizer localizer)
        {
            var requestedCulture = context.Request.Query["culture"].FirstOrDefault();
            if (localizer.IsSupportedCulture(requestedCulture ?? string.Empty))
            {
                return requestedCulture!;
            }

            if (context.Request.Cookies.TryGetValue(_options.CookieName, out var cookieCulture) &&
                localizer.IsSupportedCulture(cookieCulture))
            {
                return cookieCulture;
            }

            var browserCulture = context.Request.Headers.AcceptLanguage
                .ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => value.Split(';')[0])
                .FirstOrDefault(localizer.IsSupportedCulture);

            return browserCulture ?? _options.DefaultCulture;
        }
    }
}
