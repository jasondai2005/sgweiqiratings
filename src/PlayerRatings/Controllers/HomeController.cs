using System;
using System.Linq;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using PlayerRatings.Localization;
using PlayerRatings.ViewModels.Home;
using PlayerRatings.Models;
using PlayerRatings.Services;

namespace PlayerRatings.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizer<HomeController> _localizer;
        private readonly ILanguageData _languageData;
        private readonly IOptions<AppSettings> _settings;
        private readonly IEmailSender _emailSender;

        public HomeController(
            ApplicationDbContext context,
            IStringLocalizer<HomeController> localizer,
            ILanguageData languageData,
            IOptions<AppSettings> settings,
            IEmailSender emailSender)
        {
            _context = context;
            _localizer = localizer;
            _languageData = languageData;
            _settings = settings;
            _emailSender = emailSender;
        }

        public IActionResult Index()
        {
            var users = _context.Users.ToList();
            var matches = _context.Match.ToList();
            var userIds = matches.Select(x => x.FirstPlayerId).Distinct().Union(matches.Select(x => x.SecondPlayerId).Distinct()).ToList();
            var model = new IndexViewModel(_context.League.Count(), users.Where(x => userIds.Contains(x.Id)).Count(), matches.Count());

            return View(model);
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult RatingSystem()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(ContactViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (_settings.Value.ContactEmail != null)
                {
                    await _emailSender.SendEmailAsync(_settings.Value.ContactEmail, "Message from support page",
                        model.Message + "\n\n\n" + model.ClientContact);
                }

                ViewData["Message"] = _localizer[nameof(LocalizationKey.YourMessageIsSent)];

                return View();
            }

            return View(model);
        }

        public IActionResult Date()
        {
            return Content(DateTime.Now.ToString(CultureInfo.InvariantCulture));
        }

        public IActionResult Error()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangeLanguage(string language, string curUrl)
        {
            Response.Cookies.Append(_languageData.CookieName, language);

            return Redirect(curUrl);
        }
        
        /// <summary>
        /// Cookie name for SWA Only preference
        /// </summary>
        public const string SwaOnlyCookieName = "SwaOnly";
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleSwaOnly(string curUrl)
        {
            // Read current value and toggle it
            var currentValue = Request.Cookies[SwaOnlyCookieName] == "true";
            var newValue = !currentValue;
            
            Response.Cookies.Append(SwaOnlyCookieName, newValue.ToString().ToLower(), new Microsoft.AspNetCore.Http.CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                Path = "/",
                IsEssential = true,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax
            });

            return Redirect(curUrl);
        }
    }
}
