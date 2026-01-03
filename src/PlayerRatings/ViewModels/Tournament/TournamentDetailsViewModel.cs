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
        
        /// <summary>
        /// Additional notes about the tournament
        /// </summary>
        public string Notes { get; set; }
        
        /// <summary>
        /// External links (URLs separated by semicolons)
        /// </summary>
        public string ExternalLinks { get; set; }
        
        /// <summary>
        /// Parsed external links for display
        /// </summary>
        public List<string> ExternalLinksList => string.IsNullOrEmpty(ExternalLinks) 
            ? new List<string>() 
            : new List<string>(ExternalLinks.Split(';', StringSplitOptions.RemoveEmptyEntries));
        
        /// <summary>
        /// Photo URL/path for the tournament
        /// </summary>
        public string Photo { get; set; }
        
        /// <summary>
        /// Original standings photo URL/path
        /// </summary>
        public string StandingsPhoto { get; set; }
        
        /// <summary>
        /// Whether personal awards are supported (individual positions)
        /// </summary>
        public bool SupportsPersonalAward { get; set; }
        
        /// <summary>
        /// Whether team awards are supported
        /// </summary>
        public bool SupportsTeamAward { get; set; }
        
        /// <summary>
        /// Whether female awards are supported (requires SupportsPersonalAward)
        /// </summary>
        public bool SupportsFemaleAward { get; set; }
        
        public bool IsAdmin { get; set; }
        
        public List<TournamentMatchViewModel> Matches { get; set; } = new List<TournamentMatchViewModel>();
        
        public List<TournamentPlayerViewModel> Players { get; set; } = new List<TournamentPlayerViewModel>();
        
        /// <summary>
        /// Team standings for team award tournaments
        /// </summary>
        public List<TeamStandingViewModel> TeamStandings { get; set; } = new List<TeamStandingViewModel>();
        
        /// <summary>
        /// Maximum round number in the tournament (for standings table columns)
        /// </summary>
        public int MaxRounds { get; set; }
        
        /// <summary>
        /// First match date for each round (round number -> date)
        /// Used for creating new matches in a specific round
        /// </summary>
        public Dictionary<int, DateTimeOffset> RoundDates { get; set; } = new Dictionary<int, DateTimeOffset>();
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
        
        /// <summary>
        /// Number of wins (draws count as 0.5)
        /// </summary>
        public double Wins { get; set; }
        
        public int Losses { get; set; }
        
        /// <summary>
        /// Point differential (points for - points against)
        /// </summary>
        public int PointDiff { get; set; }
        
        /// <summary>
        /// SOS - Sum of Opponents' Scores (sum of opponents' wins, draws count as 0.5)
        /// </summary>
        public double SOS { get; set; }
        
        /// <summary>
        /// SOSOS - Sum of Opponents' SOS (sum of opponents' SOS scores)
        /// </summary>
        public double SOSOS { get; set; }
        
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
        
        /// <summary>
        /// Team name for this player in the tournament
        /// </summary>
        public string Team { get; set; }
        
        /// <summary>
        /// Team position (for top 3 players when SupportsTeamAward is true)
        /// </summary>
        public int? TeamPosition { get; set; }
        
        /// <summary>
        /// Female position (separate ranking for female players)
        /// </summary>
        public int? FemalePosition { get; set; }
        
        /// <summary>
        /// Whether the player is female (DisplayName contains '♀')
        /// </summary>
        public bool IsFemale { get; set; }
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
    
    /// <summary>
    /// View model for team standings in a tournament
    /// </summary>
    public class TeamStandingViewModel
    {
        /// <summary>
        /// Index in the team standings table
        /// </summary>
        public int Index { get; set; }
        
        /// <summary>
        /// Team's position
        /// </summary>
        public int? Position { get; set; }
        
        /// <summary>
        /// Team name
        /// </summary>
        public string TeamName { get; set; }
        
        /// <summary>
        /// Players in this team
        /// </summary>
        public List<TeamPlayerViewModel> Players { get; set; } = new List<TeamPlayerViewModel>();
        
        /// <summary>
        /// Team wins (for team-mode swiss ranking)
        /// </summary>
        public double TeamWins { get; set; }
        
        /// <summary>
        /// Team SOS (for team-mode swiss ranking)
        /// </summary>
        public double TeamSOS { get; set; }
        
        /// <summary>
        /// Team SOSOS (for team-mode swiss ranking)
        /// </summary>
        public double TeamSOSOS { get; set; }
        
        /// <summary>
        /// Total wins of all players in the team
        /// </summary>
        public double TotalPlayerWins { get; set; }
        
        /// <summary>
        /// Sum of player positions (for personal-mode team ranking)
        /// </summary>
        public int SumOfPlayerPositions { get; set; }
        
        /// <summary>
        /// Round results for team (only in team-mode)
        /// Key is round number, value contains opponent team and result
        /// </summary>
        public Dictionary<int, TeamRoundResult> TeamRoundResults { get; set; } = new Dictionary<int, TeamRoundResult>();
    }
    
    /// <summary>
    /// View model for a player within a team
    /// </summary>
    public class TeamPlayerViewModel
    {
        public string PlayerId { get; set; }
        
        public string PlayerName { get; set; }
        
        /// <summary>
        /// Player's individual position in the tournament
        /// </summary>
        public int? PersonalPosition { get; set; }
        
        /// <summary>
        /// Whether this player counts towards the team award (top 3)
        /// </summary>
        public bool CountsForTeamAward { get; set; }
        
        /// <summary>
        /// Round results for this player
        /// Key is round number
        /// </summary>
        public Dictionary<int, RoundResult> RoundResults { get; set; } = new Dictionary<int, RoundResult>();
    }
    
    /// <summary>
    /// Result for a single round in team competition
    /// </summary>
    public class TeamRoundResult
    {
        /// <summary>
        /// Opponent team's index in the standings
        /// </summary>
        public int OpponentTeamIndex { get; set; }
        
        /// <summary>
        /// Opponent team's name
        /// </summary>
        public string OpponentTeamName { get; set; }
        
        /// <summary>
        /// true = team won, false = team lost, null = draw
        /// </summary>
        public bool? Won { get; set; }
        
        /// <summary>
        /// Score display (e.g., "3:1" for individual wins)
        /// </summary>
        public string Score { get; set; }
    }
}

