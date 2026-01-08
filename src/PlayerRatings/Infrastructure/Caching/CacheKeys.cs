using System;

namespace PlayerRatings.Infrastructure.Caching
{
    /// <summary>
    /// Centralized cache key generation for consistent caching across the application.
    /// </summary>
    public static class CacheKeys
    {
        /// <summary>
        /// Cache key for a league with all its matches and players.
        /// </summary>
        public static string League(Guid leagueId) => $"League_{leagueId}";
        
        /// <summary>
        /// Cache key for league rating calculations.
        /// </summary>
        /// <param name="leagueId">The league ID.</param>
        /// <param name="cutoffDate">The date to calculate ratings up to.</param>
        /// <param name="swaOnly">Whether to filter for SWA tournaments only.</param>
        public static string LeagueRatings(Guid leagueId, DateTimeOffset cutoffDate, bool swaOnly) 
            => $"LeagueRatings_{leagueId}_{cutoffDate:yyyyMMdd}_{swaOnly}";
        
        /// <summary>
        /// Cache key for a tournament with all its data.
        /// </summary>
        public static string Tournament(Guid tournamentId) => $"Tournament_{tournamentId}";
        
        /// <summary>
        /// Cache key for tournament Swiss standings.
        /// </summary>
        public static string TournamentSwiss(Guid tournamentId) => $"TournamentSwiss_{tournamentId}";
        
        /// <summary>
        /// Cache key for tournament rating deltas.
        /// </summary>
        public static string TournamentRatingDelta(Guid tournamentId, bool swaOnly) 
            => $"TournamentRatingDelta_{tournamentId}_{swaOnly}";
        
        /// <summary>
        /// Cache key for player details.
        /// </summary>
        public static string Player(string playerId) => $"Player_{playerId}";
        
        /// <summary>
        /// Cache key for player rating history.
        /// </summary>
        /// <param name="playerId">The player ID.</param>
        /// <param name="leagueId">The league ID.</param>
        /// <param name="swaOnly">Whether to filter for SWA tournaments only.</param>
        public static string PlayerRatingHistory(string playerId, Guid leagueId, bool swaOnly) 
            => $"PlayerHistory_{playerId}_{leagueId}_{swaOnly}";
        
        /// <summary>
        /// Cache key for monthly rating snapshots.
        /// </summary>
        public static string MonthlyRatings(Guid leagueId, int year, int month, bool swaOnly) 
            => $"MonthlyRatings_{leagueId}_{year}_{month}_{swaOnly}";
    }
    
    /// <summary>
    /// Cache duration constants for different data types.
    /// </summary>
    public static class CacheDurations
    {
        /// <summary>
        /// Cache duration for league data (5 minutes).
        /// </summary>
        public static readonly TimeSpan League = TimeSpan.FromMinutes(5);
        
        /// <summary>
        /// Cache duration for rating calculations (2 minutes - shorter since ratings change).
        /// </summary>
        public static readonly TimeSpan Ratings = TimeSpan.FromMinutes(2);
        
        /// <summary>
        /// Cache duration for tournament data (5 minutes).
        /// </summary>
        public static readonly TimeSpan Tournament = TimeSpan.FromMinutes(5);
        
        /// <summary>
        /// Cache duration for static reference data (30 minutes).
        /// </summary>
        public static readonly TimeSpan Reference = TimeSpan.FromMinutes(30);
        
        /// <summary>
        /// Cache duration for player details (3 minutes).
        /// </summary>
        public static readonly TimeSpan Player = TimeSpan.FromMinutes(3);
    }
}

