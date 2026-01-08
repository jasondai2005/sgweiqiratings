using System;
using System.Collections.Generic;
using PlayerRatings.Engine.Stats;
using PlayerRatings.Models;

namespace PlayerRatings.Services.Rating
{
    /// <summary>
    /// Options for rating calculations.
    /// </summary>
    public class RatingOptions
    {
        /// <summary>
        /// Calculate ratings up to this date.
        /// </summary>
        public DateTimeOffset CutoffDate { get; set; } = DateTimeOffset.UtcNow;
        
        /// <summary>
        /// Filter for SWA tournaments only.
        /// </summary>
        public bool SwaOnly { get; set; }
        
        /// <summary>
        /// Whether this is the Singapore Weiqi league (affects filtering and visibility rules).
        /// </summary>
        public bool IsSgLeague { get; set; }
        
        /// <summary>
        /// Optional filter - only include these user IDs (null = include all).
        /// </summary>
        public HashSet<string> AllowedUserIds { get; set; }
        
        /// <summary>
        /// Force refresh from database, bypassing cache.
        /// </summary>
        public bool ForceRefresh { get; set; }
    }
    
    /// <summary>
    /// Result of a rating calculation.
    /// </summary>
    public class RatingResult
    {
        /// <summary>
        /// The EloStat containing all player ratings.
        /// </summary>
        public EloStat EloStat { get; set; }
        
        /// <summary>
        /// Set of active users (users who played matches).
        /// </summary>
        public HashSet<ApplicationUser> ActiveUsers { get; set; }
        
        /// <summary>
        /// The league that was calculated.
        /// </summary>
        public League League { get; set; }
    }
    
    /// <summary>
    /// Result of player rating lookup with ranked status.
    /// </summary>
    public class PlayerRatingInfo
    {
        /// <summary>
        /// Player ID.
        /// </summary>
        public string PlayerId { get; set; }
        
        /// <summary>
        /// Calculated rating at the cutoff date.
        /// </summary>
        public double Rating { get; set; }
        
        /// <summary>
        /// Whether the player is "ranked" (visible in ratings page) at this date.
        /// </summary>
        public bool IsRanked { get; set; }
    }
    
    /// <summary>
    /// Service for calculating and managing player ratings.
    /// Consolidates rating logic from multiple controllers.
    /// </summary>
    public interface IRatingService
    {
        /// <summary>
        /// Gets league with all matches and players (with caching).
        /// </summary>
        /// <param name="leagueId">The league ID.</param>
        /// <param name="forceRefresh">Force refresh from database.</param>
        /// <returns>The league with all related data.</returns>
        League GetLeagueWithMatches(Guid leagueId, bool forceRefresh = false);
        
        /// <summary>
        /// Calculates ratings for all players in a league.
        /// </summary>
        /// <param name="leagueId">The league ID.</param>
        /// <param name="options">Rating calculation options.</param>
        /// <returns>Rating calculation result.</returns>
        RatingResult CalculateRatings(Guid leagueId, RatingOptions options);
        
        /// <summary>
        /// Calculates ratings for the given matches (without loading from database).
        /// </summary>
        /// <param name="matches">Matches to calculate ratings from.</param>
        /// <param name="options">Rating calculation options.</param>
        /// <param name="onMatchProcessed">Optional callback for each processed match.</param>
        /// <returns>Rating calculation result.</returns>
        (EloStat eloStat, HashSet<ApplicationUser> activeUsers) CalculateRatingsFromMatches(
            IEnumerable<Match> matches,
            RatingOptions options,
            Action<Match, EloStat> onMatchProcessed = null);
        
        /// <summary>
        /// Gets player ratings at a specific date.
        /// </summary>
        /// <param name="matches">Matches to calculate from.</param>
        /// <param name="playerIds">Player IDs to get ratings for.</param>
        /// <param name="options">Rating calculation options.</param>
        /// <returns>Dictionary of player ID to rating info.</returns>
        Dictionary<string, PlayerRatingInfo> GetPlayerRatingsAtDate(
            IEnumerable<Match> matches,
            IEnumerable<string> playerIds,
            RatingOptions options);
        
        /// <summary>
        /// Gets promotion bonuses for players during a date range.
        /// </summary>
        /// <param name="matches">Matches to calculate from.</param>
        /// <param name="playerIds">Player IDs to get bonuses for.</param>
        /// <param name="startDate">Start of date range.</param>
        /// <param name="endDate">End of date range.</param>
        /// <param name="options">Rating calculation options.</param>
        /// <returns>Dictionary of player ID to total promotion bonus.</returns>
        Dictionary<string, double> GetPromotionBonuses(
            IEnumerable<Match> matches,
            IEnumerable<string> playerIds,
            DateTimeOffset startDate,
            DateTimeOffset endDate,
            RatingOptions options);
        
        /// <summary>
        /// Invalidates cached data for a league.
        /// </summary>
        /// <param name="leagueId">The league ID to invalidate.</param>
        void InvalidateLeagueCache(Guid leagueId);
    }
}

