using System;
using System.Collections.Generic;
using System.Linq;
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
        
        /// <summary>
        /// ID of the previous player in ranking order (higher position). Null if this is the first player.
        /// </summary>
        public string PreviousPlayerId { get; set; }
        
        /// <summary>
        /// ID of the next player in ranking order (lower position). Null if this is the last player.
        /// </summary>
        public string NextPlayerId { get; set; }
        
        /// <summary>
        /// Available tournaments for ranking selection dropdown.
        /// </summary>
        public List<TournamentOption> TournamentOptions { get; set; } = new List<TournamentOption>();
        
        // ===== Statistics computed from GameRecords =====
        
        /// <summary>
        /// Total number of games played (all time).
        /// </summary>
        public int TotalGames => GameRecords?.Count ?? 0;
        
        /// <summary>
        /// Total wins (all time).
        /// </summary>
        public int TotalWins => GameRecords?.Count(g => g.Result == "Win") ?? 0;
        
        /// <summary>
        /// Total losses (all time).
        /// </summary>
        public int TotalLosses => GameRecords?.Count(g => g.Result == "Loss") ?? 0;
        
        /// <summary>
        /// Overall win rate as a percentage (0-100).
        /// </summary>
        public double OverallWinRate => TotalGames > 0 ? (double)TotalWins / TotalGames * 100 : 0;
        
        /// <summary>
        /// Games played in the current calendar year.
        /// </summary>
        public int CurrentYearGames => GameRecords?.Count(g => g.Date.Year == DateTime.Now.Year) ?? 0;
        
        /// <summary>
        /// Wins in the current calendar year.
        /// </summary>
        public int CurrentYearWins => GameRecords?.Count(g => g.Date.Year == DateTime.Now.Year && g.Result == "Win") ?? 0;
        
        /// <summary>
        /// Losses in the current calendar year.
        /// </summary>
        public int CurrentYearLosses => GameRecords?.Count(g => g.Date.Year == DateTime.Now.Year && g.Result == "Loss") ?? 0;
        
        /// <summary>
        /// Win rate for the current calendar year as a percentage (0-100).
        /// </summary>
        public double CurrentYearWinRate => CurrentYearGames > 0 ? (double)CurrentYearWins / CurrentYearGames * 100 : 0;
        
        /// <summary>
        /// Games played in the previous calendar year.
        /// </summary>
        public int LastYearGames => GameRecords?.Count(g => g.Date.Year == DateTime.Now.Year - 1) ?? 0;
        
        /// <summary>
        /// Wins in the previous calendar year.
        /// </summary>
        public int LastYearWins => GameRecords?.Count(g => g.Date.Year == DateTime.Now.Year - 1 && g.Result == "Win") ?? 0;
        
        /// <summary>
        /// Losses in the previous calendar year.
        /// </summary>
        public int LastYearLosses => GameRecords?.Count(g => g.Date.Year == DateTime.Now.Year - 1 && g.Result == "Loss") ?? 0;
        
        /// <summary>
        /// Win rate for the previous calendar year as a percentage (0-100).
        /// </summary>
        public double LastYearWinRate => LastYearGames > 0 ? (double)LastYearWins / LastYearGames * 100 : 0;
    }

    public class MonthlyRating
    {
        public DateTime Month { get; set; }
        public string MonthDisplay => Month.ToString("MMM yyyy");
        public double Rating { get; set; }
        public string RatingDisplay => Rating.ToString("F1");
        public int MatchesInMonth { get; set; }
        public List<string> MatchNames { get; set; } = new List<string>();
        
        /// <summary>
        /// Player's position in the ranking at the end of this month (1-based). 0 means not ranked.
        /// </summary>
        public int Position { get; set; }
        
        /// <summary>
        /// Total number of ranked players at the end of this month.
        /// </summary>
        public int TotalPlayers { get; set; }
        
        /// <summary>
        /// Position display string (e.g., "№3" or "-" if not ranked).
        /// </summary>
        public string PositionDisplay => Position > 0 ? $"№{Position}" : "-";
        
        /// <summary>
        /// Promotion bonuses applied this month: list of (fromRanking, fromOrg, toRanking, toOrg, bonusAmount, promotionDate).
        /// </summary>
        public List<(string FromRanking, string FromOrg, string ToRanking, string ToOrg, double BonusAmount, DateTimeOffset? PromotionDate)> PromotionBonuses { get; set; } 
            = new List<(string, string, string, string, double, DateTimeOffset?)>();
        
        /// <summary>
        /// Whether this month had any promotion bonuses applied.
        /// </summary>
        public bool HasPromotionBonus => PromotionBonuses.Any();
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
        
        /// <summary>
        /// Match factor. 0 means the match is not rated.
        /// </summary>
        public double? Factor { get; set; }
        
        /// <summary>
        /// Whether this match is rated (factor != 0).
        /// </summary>
        public bool IsRated => Factor != 0;
    }
}

