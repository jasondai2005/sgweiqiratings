using System;
using System.Collections.Generic;
using PlayerRatings.Models;

namespace PlayerRatings.ViewModels.Tournament
{
    /// <summary>
    /// View model for tournament details page
    /// </summary>
    public class TournamentDetailsViewModel
    {
        public Guid Id { get; set; }
        
        public Guid LeagueId { get; set; }
        
        public string LeagueName { get; set; }
        
        public string Name { get; set; }
        
        public string Ordinal { get; set; }
        
        public string Group { get; set; }
        
        public string FullName { get; set; }
        
        public string Organizer { get; set; }
        
        public string Location { get; set; }
        
        public DateTimeOffset? StartDate { get; set; }
        
        public DateTimeOffset? EndDate { get; set; }
        
        public string TournamentType { get; set; }
        
        public double? Factor { get; set; }
        
        public bool IsAdmin { get; set; }
        
        public List<TournamentMatchViewModel> Matches { get; set; } = new List<TournamentMatchViewModel>();
        
        public List<TournamentPlayerViewModel> Players { get; set; } = new List<TournamentPlayerViewModel>();
    }
    
    /// <summary>
    /// View model for a match within a tournament
    /// </summary>
    public class TournamentMatchViewModel
    {
        public Guid Id { get; set; }
        
        public DateTimeOffset Date { get; set; }
        
        public int? Round { get; set; }
        
        public string FirstPlayerId { get; set; }
        
        public string FirstPlayerName { get; set; }
        
        public string SecondPlayerId { get; set; }
        
        public string SecondPlayerName { get; set; }
        
        public int FirstPlayerScore { get; set; }
        
        public int SecondPlayerScore { get; set; }
        
        public double? Factor { get; set; }
        
        public string MatchName { get; set; }
    }
    
    /// <summary>
    /// View model for a player within a tournament
    /// </summary>
    public class TournamentPlayerViewModel
    {
        public string PlayerId { get; set; }
        
        public string PlayerName { get; set; }
        
        public int? Position { get; set; }
        
        public Guid? PromotionId { get; set; }
        
        public string PromotionRanking { get; set; }
        
        public int MatchCount { get; set; }
        
        public int Wins { get; set; }
        
        public int Losses { get; set; }
    }
}

