using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SIS.Services.Localization;

namespace SIS.Controllers
{
    [AllowAnonymous]
    public class LocalizationController : Controller
    {
        private readonly IJsonStringLocalizer _localizer;
        private readonly JsonLocalizationOptions _options;

        public LocalizationController(IJsonStringLocalizer localizer, IOptions<JsonLocalizationOptions> options)
        {
            _localizer = localizer;
            _options = options.Value;
        }

        [HttpGet]
        public IActionResult Set(string culture, string? returnUrl = null)
        {
            if (_localizer.IsSupportedCulture(culture))
            {
                Response.Cookies.Append(_options.CookieName, culture, new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = false,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps
                });
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Login", "Account");
        }
    }
}
