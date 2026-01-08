using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PlayerRatings.Models;
using PlayerRatings.Util;

namespace PlayerRatings.Controllers
{
    /// <summary>
    /// Base controller providing common functionality for all controllers.
    /// </summary>
    public abstract class BaseController : Controller
    {
        protected readonly UserManager<ApplicationUser> UserManager;

        protected BaseController(UserManager<ApplicationUser> userManager)
        {
            UserManager = userManager;
        }

        /// <summary>
        /// Cookie name for SWA Only preference.
        /// </summary>
        public const string SwaOnlyCookieName = "SwaOnly";

        /// <summary>
        /// Gets the SWA Only preference from cookie.
        /// </summary>
        protected bool GetSwaOnlyPreference()
        {
            return Request.Cookies[SwaOnlyCookieName] == "true";
        }

        /// <summary>
        /// Sets the SWA Only preference cookie.
        /// </summary>
        protected void SetSwaOnlyPreference(bool value)
        {
            Response.Cookies.Append(SwaOnlyCookieName, value.ToString().ToLower(), new Microsoft.AspNetCore.Http.CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                Path = "/",
                IsEssential = true,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax
            });
        }

        /// <summary>
        /// Gets the current authenticated user.
        /// </summary>
        protected async Task<ApplicationUser> GetCurrentUserAsync()
        {
            return await User.GetApplicationUser(UserManager);
        }

        /// <summary>
        /// Checks if the specified league is the Singapore Weiqi league.
        /// </summary>
        protected static bool IsSgLeague(string leagueName)
        {
            return leagueName?.Contains("Singapore Weiqi") ?? false;
        }

        /// <summary>
        /// Checks if the specified league is the Singapore Weiqi league.
        /// </summary>
        protected static bool IsSgLeague(League league)
        {
            return IsSgLeague(league?.Name);
        }

        /// <summary>
        /// Sets ViewData for league type to control navbar toggle visibility.
        /// </summary>
        protected void SetLeagueTypeViewData(bool isSgLeague)
        {
            ViewData["IsSgLeague"] = isSgLeague;
        }

        /// <summary>
        /// Sets ViewData for league type and returns whether SWA filter should be applied.
        /// </summary>
        protected bool SetLeagueContextAndGetSwaOnly(string leagueName)
        {
            bool isSg = IsSgLeague(leagueName);
            SetLeagueTypeViewData(isSg);
            return isSg && GetSwaOnlyPreference();
        }

        /// <summary>
        /// Sets ViewData for league type and returns whether SWA filter should be applied.
        /// </summary>
        protected bool SetLeagueContextAndGetSwaOnly(League league)
        {
            return SetLeagueContextAndGetSwaOnly(league?.Name);
        }

        /// <summary>
        /// Sets a success message in TempData.
        /// </summary>
        protected void SetSuccessMessage(string message)
        {
            TempData["SuccessMessage"] = message;
        }

        /// <summary>
        /// Sets an error message in TempData.
        /// </summary>
        protected void SetErrorMessage(string message)
        {
            TempData["ErrorMessage"] = message;
        }
    }
}

