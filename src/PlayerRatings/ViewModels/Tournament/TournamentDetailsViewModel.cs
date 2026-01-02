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
        
        /// <summary>
        /// Maximum round number in the tournament (for standings table columns)
        /// </summary>
        public int MaxRounds { get; set; }
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
        
        public string FirstPlayerRanking { get; set; }
        
        public string SecondPlayerId { get; set; }
        
        public string SecondPlayerName { get; set; }
        
        public string SecondPlayerRanking { get; set; }
        
        public int FirstPlayerScore { get; set; }
        
        public int SecondPlayerScore { get; set; }
        
        public double? Factor { get; set; }
        
        public string MatchName { get; set; }
        
        /// <summary>
        /// First player's rating before this match (from ELO calculation)
        /// </summary>
        public string FirstPlayerRatingBefore { get; set; }
        
        /// <summary>
        /// Second player's rating before this match (from ELO calculation)
        /// </summary>
        public string SecondPlayerRatingBefore { get; set; }
        
        /// <summary>
        /// Rating shift for the match (winner gains, loser loses)
        /// </summary>
        public string ShiftRating { get; set; }
        
        public string GetScore() => $"{FirstPlayerScore} : {SecondPlayerScore}";
    }
    
    /// <summary>
    /// View model for a player within a tournament
    /// </summary>
    public class TournamentPlayerViewModel
    {
        public string PlayerId { get; set; }
        
        public string PlayerName { get; set; }
        
        /// <summary>
        /// Player's ranking before/at the start of the tournament
        /// </summary>
        public string PlayerRanking { get; set; }
        
        /// <summary>
        /// Player's residence/location
        /// </summary>
        public string Residence { get; set; }
        
        /// <summary>
        /// The saved position in database (null if not set)
        /// </summary>
        public int? Position { get; set; }
        
        /// <summary>
        /// The calculated position based on wins/point differential
        /// </summary>
        public int CalculatedPosition { get; set; }
        
        /// <summary>
        /// Returns the saved position, or calculated position if not saved
        /// </summary>
        public int DisplayPosition => Position ?? CalculatedPosition;
        
        /// <summary>
        /// True if position is calculated (not saved in database)
        /// </summary>
        public bool IsCalculatedPosition => !Position.HasValue;
        
        public Guid? PromotionId { get; set; }
        
        public string PromotionRanking { get; set; }
        
        /// <summary>
        /// Promotion bonus amount awarded (e.g., 3.2 for +3.2 rating boost)
        /// </summary>
        public double? PromotionBonus { get; set; }
        
        /// <summary>
        /// Display string for promotion with bonus, e.g., "1D +3.2"
        /// </summary>
        public string PromotionDisplay => !string.IsNullOrEmpty(PromotionRanking) 
            ? (PromotionBonus.HasValue ? $"{PromotionRanking} +{PromotionBonus.Value:F1}" : PromotionRanking)
            : null;
        
        public int MatchCount { get; set; }
        
        public int Wins { get; set; }
        
        public int Losses { get; set; }
        
        /// <summary>
        /// Point differential (points for - points against)
        /// </summary>
        public int PointDiff { get; set; }
        
        /// <summary>
        /// SOS - Sum of Opponents' Scores (sum of opponents' wins)
        /// </summary>
        public int SOS { get; set; }
        
        /// <summary>
        /// SOSOS - Sum of Opponents' SOS (sum of opponents' SOS scores)
        /// </summary>
        public int SOSOS { get; set; }
        
        /// <summary>
        /// Player's ELO rating before the tournament
        /// </summary>
        public double? RatingBefore { get; set; }
        
        /// <summary>
        /// Player's ELO rating after the tournament
        /// </summary>
        public double? RatingAfter { get; set; }
        
        /// <summary>
        /// Whether the player had an official ranking before the tournament
        /// </summary>
        public bool WasRankedBefore { get; set; }
        
        /// <summary>
        /// Whether the player has an official ranking after the tournament
        /// </summary>
        public bool IsRankedAfter { get; set; }
        
        /// <summary>
        /// Whether to show the "before" rating (player was ranked before tournament)
        /// </summary>
        public bool ShowRatingBefore => WasRankedBefore;
        
        /// <summary>
        /// Whether to show the "after" rating (player is ranked after tournament)
        /// </summary>
        public bool ShowRatingAfter => IsRankedAfter;
        
        /// <summary>
        /// Rating change during the tournament (RatingAfter - RatingBefore)
        /// Only calculated if player was ranked both before and after
        /// </summary>
        public double? RatingChange => WasRankedBefore && IsRankedAfter && RatingBefore.HasValue && RatingAfter.HasValue 
            ? RatingAfter.Value - RatingBefore.Value 
            : null;
        
        /// <summary>
        /// True if player is undefeated (0 losses) - champion status
        /// </summary>
        public bool IsUndefeated => Losses == 0 && Wins > 0;
        
        /// <summary>
        /// Round results - key is round number, value is the result
        /// </summary>
        public Dictionary<int, RoundResult> RoundResults { get; set; } = new Dictionary<int, RoundResult>();
    }
    
    /// <summary>
    /// Result for a single round
    /// </summary>
    public class RoundResult
    {
        /// <summary>
        /// Opponent's player ID
        /// </summary>
        public string OpponentId { get; set; }
        
        /// <summary>
        /// Opponent's display name
        /// </summary>
        public string OpponentName { get; set; }
        
        /// <summary>
        /// True if won, false if lost, null if draw
        /// </summary>
        public bool? Won { get; set; }
        
        /// <summary>
        /// Score display (e.g., "2:1")
        /// </summary>
        public string Score { get; set; }
    }
}

