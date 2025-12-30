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

        /// <summary>
        /// Indicates if the current user is an admin for this league
        /// </summary>
        public bool IsAdmin { get; set; }
    }
}
