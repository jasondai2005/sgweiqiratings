using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlayerRatings.Models;
using PlayerRatings.Services.Swiss;
using PlayerRatings.ViewModels.Tournament;

namespace PlayerRatings.Services.Tournament
{
    /// <summary>
    /// Options for loading tournament data.
    /// </summary>
    public class TournamentLoadOptions
    {
        /// <summary>
        /// Include match data.
        /// </summary>
        public bool IncludeMatches { get; set; } = true;
        
        /// <summary>
        /// Include player data.
        /// </summary>
        public bool IncludePlayers { get; set; } = true;
        
        /// <summary>
        /// Include rating calculations.
        /// </summary>
        public bool CalculateRatings { get; set; } = true;
        
        /// <summary>
        /// Force refresh from database.
        /// </summary>
        public bool ForceRefresh { get; set; }
        
        /// <summary>
        /// SWA only filter preference.
        /// </summary>
        public bool SwaOnly { get; set; }
    }
    
    /// <summary>
    /// Result of tournament rating delta calculation.
    /// </summary>
    public class TournamentRatingDelta
    {
        /// <summary>
        /// Player ratings before the tournament.
        /// </summary>
        public Dictionary<string, double> RatingsBefore { get; set; } = new Dictionary<string, double>();
        
        /// <summary>
        /// Player ratings after the tournament.
        /// </summary>
        public Dictionary<string, double> RatingsAfter { get; set; } = new Dictionary<string, double>();
        
        /// <summary>
        /// Promotion bonuses received during the tournament.
        /// </summary>
        public Dictionary<string, double> PromotionBonuses { get; set; } = new Dictionary<string, double>();
    }
    
    /// <summary>
    /// Service for tournament-related operations.
    /// </summary>
    public interface ITournamentService
    {
        /// <summary>
        /// Gets a tournament by ID with all related data.
        /// </summary>
        /// <param name="tournamentId">The tournament ID.</param>
        /// <param name="options">Loading options.</param>
        /// <returns>The tournament with related data, or null if not found.</returns>
        Task<Models.Tournament> GetTournamentAsync(Guid tournamentId, TournamentLoadOptions options = null);
        
        /// <summary>
        /// Checks if the specified league is the Singapore Weiqi league.
        /// </summary>
        /// <param name="leagueName">The league name.</param>
        /// <returns>True if it's the SG league.</returns>
        bool IsSgLeague(string leagueName);
        
        /// <summary>
        /// Calculates rating deltas for a tournament.
        /// </summary>
        /// <param name="tournament">The tournament (with matches loaded).</param>
        /// <param name="leagueMatches">All matches in the league (for rating calculation).</param>
        /// <param name="isSgLeague">Whether this is the SG league.</param>
        /// <param name="swaOnly">SWA only filter.</param>
        /// <returns>Rating delta results.</returns>
        TournamentRatingDelta CalculateRatingDeltas(
            Models.Tournament tournament,
            IEnumerable<Match> leagueMatches,
            bool isSgLeague,
            bool swaOnly);
        
        /// <summary>
        /// Calculates Swiss system standings for a tournament.
        /// </summary>
        /// <param name="matches">Tournament matches.</param>
        /// <returns>Swiss statistics.</returns>
        SwissStats CalculateSwissStandings(IEnumerable<Match> matches);
        
        // Note: Team standings calculation remains in controller due to 
        // complex dependencies on existing ViewModel structure
        
        /// <summary>
        /// Gets the tournament full name (combining ordinal, name, and group).
        /// </summary>
        /// <param name="tournament">The tournament.</param>
        /// <returns>Full display name.</returns>
        string GetTournamentFullName(Models.Tournament tournament);
        
        /// <summary>
        /// Invalidates cached data for a tournament.
        /// </summary>
        /// <param name="tournamentId">The tournament ID.</param>
        void InvalidateTournamentCache(Guid tournamentId);
    }
}

