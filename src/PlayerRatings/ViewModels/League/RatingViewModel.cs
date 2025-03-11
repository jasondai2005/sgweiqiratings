using System.Collections.Generic;
using PlayerRatings.Engine.Stats;
using PlayerRatings.Models;

namespace PlayerRatings.ViewModels.League
{
    public class RatingViewModel
    {
        public RatingViewModel(IEnumerable<IStat> stats, IEnumerable<ApplicationUser> users, IEnumerable<ApplicationUser> promotedPlayers,
            IEnumerable<Models.Match> lastMatches, Dictionary<string, Dictionary<string, string>> forecast)
        {
            Stats = stats;
            Users = users;
            PromotedPlayers = promotedPlayers;
            Forecast = forecast;
            LastMatches = lastMatches;
        }

        public IEnumerable<IStat> Stats { get; private set; }

        public IEnumerable<ApplicationUser> Users { get; private set; }

        public IEnumerable<ApplicationUser> PromotedPlayers { get; private set; }

        public Dictionary<string, Dictionary<string, string>> Forecast { get; private set; }

        public IEnumerable<Models.Match> LastMatches { get; private set; }
    }
}
