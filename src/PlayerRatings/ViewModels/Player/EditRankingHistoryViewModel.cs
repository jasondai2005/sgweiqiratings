using System;
using System.Collections.Generic;
using PlayerRatings.Models;

namespace PlayerRatings.ViewModels.Player
{
    public class EditRankingHistoryViewModel
    {
        public ApplicationUser Player { get; set; }
        public Guid LeagueId { get; set; }
        public string LeagueName { get; set; }
        
        /// <summary>
        /// Player's ranking history records.
        /// </summary>
        public List<PlayerRanking> Rankings { get; set; } = new List<PlayerRanking>();
        
        /// <summary>
        /// Available tournaments for ranking selection dropdown.
        /// </summary>
        public List<TournamentOption> TournamentOptions { get; set; } = new List<TournamentOption>();
        
        /// <summary>
        /// Optional: Pre-select a tournament when opening from Tournament Details page.
        /// </summary>
        public Guid? PreselectedTournamentId { get; set; }
    }
}

