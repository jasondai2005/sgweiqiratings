using System;
using System.Collections.Generic;
using PlayerRatings.Models;

namespace PlayerRatings.ViewModels.League
{
    public class DeletePlayerViewModel
    {
        public Guid LeagueId { get; set; }
        
        public string LeagueName { get; set; }
        
        public string PlayerId { get; set; }
        
        public string PlayerName { get; set; }
        
        public string PlayerUsername { get; set; }
        
        public int MatchCount { get; set; }
        
        public List<Models.Match> Matches { get; set; } = new List<Models.Match>();
        
        public int TournamentCount { get; set; }
        
        public int RankingCount { get; set; }
    }
}

