using System.Collections.Generic;
using PlayerRatings.Models;

namespace PlayerRatings.ViewModels.League
{
    public class LeagueDetailsViewModel
    {
        internal bool SwaRankedPlayersOnly { get; set; }

        public Models.League League { get; set; }

        public IEnumerable<LeaguePlayer> Players { get; set; }

        public IEnumerable<LeaguePlayer> NonLocalPlayers { get; set; } = new List<LeaguePlayer>();
    }
}
