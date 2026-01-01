using System;
using System.Collections.Generic;
using PlayerRatings.Engine.Stats;
using PlayerRatings.Models;

namespace PlayerRatings.ViewModels.League
{
    public class RatingViewModel
    {
        public RatingViewModel(IEnumerable<IStat> stats, IEnumerable<ApplicationUser> users, IEnumerable<ApplicationUser> promotedPlayers,
            IEnumerable<Models.Match> lastMatches,
            Guid leagueId, string byDate = null, bool swaOnly = false, bool isSgLeague = true,
            IEnumerable<ApplicationUser> nonLocalUsers = null,
            IEnumerable<ApplicationUser> inactiveUsers = null,
            Dictionary<string, double> previousRatings = null,
            Dictionary<string, int> previousPositions = null,
            DateTimeOffset? comparisonDate = null)
        {
            Stats = stats;
            Users = users;
            PromotedPlayers = promotedPlayers;
            LastMatches = lastMatches;
            LeagueId = leagueId;
            ByDate = byDate;
            SwaOnly = swaOnly;
            IsSgLeague = isSgLeague;
            NonLocalUsers = nonLocalUsers ?? new List<ApplicationUser>();
            InactiveUsers = inactiveUsers ?? new List<ApplicationUser>();
            PreviousRatings = previousRatings ?? new Dictionary<string, double>();
            PreviousPositions = previousPositions ?? new Dictionary<string, int>();
            ComparisonDate = comparisonDate;
        }

        public IEnumerable<IStat> Stats { get; private set; }

        public IEnumerable<ApplicationUser> Users { get; private set; }

        public IEnumerable<ApplicationUser> PromotedPlayers { get; private set; }

        public IEnumerable<ApplicationUser> NonLocalUsers { get; private set; }

        public IEnumerable<ApplicationUser> InactiveUsers { get; private set; }

        public IEnumerable<Models.Match> LastMatches { get; private set; }

        public Guid LeagueId { get; private set; }

        public string ByDate { get; private set; }

        public bool SwaOnly { get; private set; }

        public bool IsSgLeague { get; private set; }
        
        /// <summary>
        /// Previous ratings by user ID (at comparison date).
        /// </summary>
        public Dictionary<string, double> PreviousRatings { get; private set; }
        
        /// <summary>
        /// Previous positions by user ID (at comparison date, 1-based).
        /// </summary>
        public Dictionary<string, int> PreviousPositions { get; private set; }
        
        /// <summary>
        /// The date used for comparison (first day of current/previous month).
        /// </summary>
        public DateTimeOffset? ComparisonDate { get; private set; }
    }
}
