using System;
using System.Collections.Generic;
using PlayerRatings.Models;

namespace PlayerRatings.ViewModels.Player
{
    public class PlayerRatingHistoryViewModel
    {
        public ApplicationUser Player { get; set; }
        public Guid LeagueId { get; set; }
        public List<MonthlyRating> MonthlyRatings { get; set; } = new List<MonthlyRating>();
        public List<GameRecord> GameRecords { get; set; } = new List<GameRecord>();
        public bool SwaOnly { get; set; }
        public bool IsSgLeague { get; set; }
        
        /// <summary>
        /// Player's current position in the league ranking (1-based). 0 means not ranked (hidden/new player).
        /// </summary>
        public int Position { get; set; }
        
        /// <summary>
        /// Total number of ranked players in the league.
        /// </summary>
        public int TotalPlayers { get; set; }
        
        /// <summary>
        /// Position display string (e.g., "№3" or "-" if not ranked).
        /// </summary>
        public string PositionDisplay => Position > 0 ? $"№{Position}" : "-";
    }

    public class MonthlyRating
    {
        public DateTime Month { get; set; }
        public string MonthDisplay => Month.ToString("MMM yyyy");
        public double Rating { get; set; }
        public string RatingDisplay => Rating.ToString("F1");
        public int MatchesInMonth { get; set; }
        public List<string> MatchNames { get; set; } = new List<string>();
    }

    public class GameRecord
    {
        public DateTimeOffset Date { get; set; }
        public string DateDisplay => Date.ToString("dd/MM/yyyy");
        public string MatchName { get; set; }
        public string OpponentName { get; set; }
        public string OpponentRanking { get; set; }
        public string OpponentId { get; set; }
        public string Result { get; set; }
    }
}

