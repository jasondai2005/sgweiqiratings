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
        
        /// <summary>
        /// All tournaments the player has participated in (from TournamentPlayer entries).
        /// Includes tournaments where matches may not be recorded.
        /// </summary>
        public List<TournamentParticipation> TournamentParticipations { get; set; } = new List<TournamentParticipation>();
        
        // ===== Statistics computed from TournamentParticipations (more accurate than GameRecords) =====
        
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
        /// Number of tournament championships in the current calendar year (Position = 1).
        /// </summary>
        public int CurrentYearChampionshipCount => GameRecords?
            .Where(g => g.Date.Year == DateTime.Now.Year && g.TournamentPosition == 1)
            .Select(g => g.TournamentId)
            .Distinct()
            .Count() ?? 0;
        
        /// <summary>
        /// Number of team championships in the current calendar year (TeamPosition = 1).
        /// </summary>
        public int CurrentYearTeamChampionshipCount => GameRecords?
            .Where(g => g.Date.Year == DateTime.Now.Year && g.TeamPosition == 1)
            .Select(g => g.TournamentId)
            .Distinct()
            .Count() ?? 0;
        
        /// <summary>
        /// Number of female championships in the current calendar year (FemalePosition = 1).
        /// </summary>
        public int CurrentYearFemaleChampionshipCount => GameRecords?
            .Where(g => g.Date.Year == DateTime.Now.Year && g.FemalePosition == 1)
            .Select(g => g.TournamentId)
            .Distinct()
            .Count() ?? 0;
        
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
        
        /// <summary>
        /// Number of tournament championships in the previous calendar year (Position = 1).
        /// </summary>
        public int LastYearChampionshipCount => GameRecords?
            .Where(g => g.Date.Year == DateTime.Now.Year - 1 && g.TournamentPosition == 1)
            .Select(g => g.TournamentId)
            .Distinct()
            .Count() ?? 0;
        
        /// <summary>
        /// Number of team championships in the previous calendar year (TeamPosition = 1).
        /// </summary>
        public int LastYearTeamChampionshipCount => GameRecords?
            .Where(g => g.Date.Year == DateTime.Now.Year - 1 && g.TeamPosition == 1)
            .Select(g => g.TournamentId)
            .Distinct()
            .Count() ?? 0;
        
        /// <summary>
        /// Number of female championships in the previous calendar year (FemalePosition = 1).
        /// </summary>
        public int LastYearFemaleChampionshipCount => GameRecords?
            .Where(g => g.Date.Year == DateTime.Now.Year - 1 && g.FemalePosition == 1)
            .Select(g => g.TournamentId)
            .Distinct()
            .Count() ?? 0;
        
        /// <summary>
        /// Number of tournament championships (Position = 1).
        /// </summary>
        public int ChampionshipCount { get; set; }
        
        /// <summary>
        /// Number of team championships (TeamPosition = 1).
        /// </summary>
        public int TeamChampionshipCount { get; set; }
        
        /// <summary>
        /// Number of female championships (FemalePosition = 1).
        /// </summary>
        public int FemaleChampionshipCount { get; set; }
        
        // ===== Tournament count statistics (from TournamentParticipations) =====
        
        /// <summary>
        /// Total number of tournaments participated in (all time).
        /// </summary>
        public int TotalTournaments => TournamentParticipations?.Count ?? 0;
        
        /// <summary>
        /// Tournaments participated in the current calendar year.
        /// </summary>
        public int CurrentYearTournaments => TournamentParticipations?.Count(t => t.StartDate.Year == DateTime.Now.Year) ?? 0;
        
        /// <summary>
        /// Tournaments participated in the previous calendar year.
        /// </summary>
        public int LastYearTournaments => TournamentParticipations?.Count(t => t.StartDate.Year == DateTime.Now.Year - 1) ?? 0;
        
        // Override championship counts to use TournamentParticipations for more accuracy
        // (includes tournaments where matches are not recorded)
        
        /// <summary>
        /// Championships in the current calendar year (from TournamentParticipations).
        /// </summary>
        public int CurrentYearChampionships => TournamentParticipations?
            .Where(t => t.StartDate.Year == DateTime.Now.Year && t.Position == 1)
            .Count() ?? 0;
        
        /// <summary>
        /// Team championships in the current calendar year (from TournamentParticipations).
        /// </summary>
        public int CurrentYearTeamChampionships => TournamentParticipations?
            .Where(t => t.StartDate.Year == DateTime.Now.Year && t.TeamPosition == 1)
            .Count() ?? 0;
        
        /// <summary>
        /// Female championships in the current calendar year (from TournamentParticipations).
        /// </summary>
        public int CurrentYearFemaleChampionships => TournamentParticipations?
            .Where(t => t.StartDate.Year == DateTime.Now.Year && t.FemalePosition == 1)
            .Count() ?? 0;
        
        /// <summary>
        /// Championships in the previous calendar year (from TournamentParticipations).
        /// </summary>
        public int LastYearChampionships => TournamentParticipations?
            .Where(t => t.StartDate.Year == DateTime.Now.Year - 1 && t.Position == 1)
            .Count() ?? 0;
        
        /// <summary>
        /// Team championships in the previous calendar year (from TournamentParticipations).
        /// </summary>
        public int LastYearTeamChampionships => TournamentParticipations?
            .Where(t => t.StartDate.Year == DateTime.Now.Year - 1 && t.TeamPosition == 1)
            .Count() ?? 0;
        
        /// <summary>
        /// Female championships in the previous calendar year (from TournamentParticipations).
        /// </summary>
        public int LastYearFemaleChampionships => TournamentParticipations?
            .Where(t => t.StartDate.Year == DateTime.Now.Year - 1 && t.FemalePosition == 1)
            .Count() ?? 0;
    }
    
    /// <summary>
    /// Tournament participation info (from TournamentPlayer entries)
    /// </summary>
    public class TournamentParticipation
    {
        public Guid TournamentId { get; set; }
        public string TournamentName { get; set; }
        public DateTimeOffset StartDate { get; set; }
        public int? Position { get; set; }
        public int? FemalePosition { get; set; }
        public int? TeamPosition { get; set; }
        
        /// <summary>
        /// Whether this tournament has match records for the player.
        /// </summary>
        public bool HasMatches { get; set; }
    }

    public class MonthlyRating
    {
        public DateTime Month { get; set; }
        public string MonthDisplay => Month.ToString("MMM yyyy");
        public double Rating { get; set; }
        public string RatingDisplay => Rating.ToString("F1");
        public int MatchesInMonth { get; set; }
        public List<MatchInfo> Matches { get; set; } = new List<MatchInfo>();
        
        /// <summary>
        /// Legacy property for backwards compatibility - returns just match names
        /// </summary>
        public List<string> MatchNames => Matches.Select(m => m.DisplayName).ToList();
        
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
    
    /// <summary>
    /// Match info for display in rating history
    /// </summary>
    public class MatchInfo
    {
        public string MatchName { get; set; }
        public Guid? TournamentId { get; set; }
        public string TournamentName { get; set; }
        public int? Round { get; set; }
        
        /// <summary>
        /// Player's position in the tournament (1-based, null if not available)
        /// </summary>
        public int? TournamentPosition { get; set; }
        
        /// <summary>
        /// Player's female position in the tournament (1-based, null if not available or not female)
        /// </summary>
        public int? FemalePosition { get; set; }
        
        /// <summary>
        /// Player's team position in the tournament (1-based, null if not available or no team)
        /// </summary>
        public int? TeamPosition { get; set; }
        
        /// <summary>
        /// Display name - tournament name if available, otherwise match name
        /// </summary>
        public string DisplayName => TournamentName ?? MatchName ?? "";
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
        
        /// <summary>
        /// Tournament ID if match belongs to a tournament.
        /// </summary>
        public Guid? TournamentId { get; set; }
        
        /// <summary>
        /// Tournament full name for display.
        /// </summary>
        public string TournamentName { get; set; }
        
        /// <summary>
        /// Round number within the tournament.
        /// </summary>
        public int? Round { get; set; }
        
        /// <summary>
        /// Player's position in the tournament (1-based, null if not available).
        /// </summary>
        public int? TournamentPosition { get; set; }
        
        /// <summary>
        /// Player's female position in the tournament (1-based, null if not available or not female).
        /// </summary>
        public int? FemalePosition { get; set; }
        
        /// <summary>
        /// Player's team position in the tournament (1-based, null if not available or no team).
        /// </summary>
        public int? TeamPosition { get; set; }
    }
}

