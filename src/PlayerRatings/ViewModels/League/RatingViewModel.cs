using System;
using System.Collections.Generic;
using PlayerRatings.Engine.Stats;
using PlayerRatings.Models;

namespace PlayerRatings.ViewModels.League
{
    public class RatingViewModel
    {
        public RatingViewModel(IEnumerable<IStat> stats, IEnumerable<ApplicationUser> users, IEnumerable<ApplicationUser> promotedPlayers,
            IEnumerable<Models.Match> lastMatches, Dictionary<string, Dictionary<string, string>> forecast,
            Guid leagueId, string byDate = null, bool swaOnly = false, bool isIntlLeague = false, bool promotionBonus = true,
            IEnumerable<ApplicationUser> nonLocalUsers = null, bool showNonLocal = false,
            IEnumerable<ApplicationUser> inactiveUsers = null)
        {
            Stats = stats;
            Users = users;
            PromotedPlayers = promotedPlayers;
            Forecast = forecast;
            LastMatches = lastMatches;
            LeagueId = leagueId;
            ByDate = byDate;
            SwaOnly = swaOnly;
            IsIntlLeague = isIntlLeague;
            PromotionBonus = promotionBonus;
            NonLocalUsers = nonLocalUsers ?? new List<ApplicationUser>();
            ShowNonLocal = showNonLocal;
            InactiveUsers = inactiveUsers ?? new List<ApplicationUser>();
        }

        public IEnumerable<IStat> Stats { get; private set; }

        public IEnumerable<ApplicationUser> Users { get; private set; }

        public IEnumerable<ApplicationUser> PromotedPlayers { get; private set; }

        public IEnumerable<ApplicationUser> NonLocalUsers { get; private set; }

        public IEnumerable<ApplicationUser> InactiveUsers { get; private set; }

        public Dictionary<string, Dictionary<string, string>> Forecast { get; private set; }

        public IEnumerable<Models.Match> LastMatches { get; private set; }

        public Guid LeagueId { get; private set; }

        public string ByDate { get; private set; }

        public bool SwaOnly { get; private set; }

        public bool IsIntlLeague { get; private set; }

        public bool PromotionBonus { get; private set; }

        public bool ShowNonLocal { get; private set; }
    }
}
