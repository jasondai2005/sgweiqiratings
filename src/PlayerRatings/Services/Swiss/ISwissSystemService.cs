using System.Collections.Generic;
using PlayerRatings.Models;

namespace PlayerRatings.Services.Swiss
{
    /// <summary>
    /// Statistics for a single player in a Swiss tournament.
    /// </summary>
    public class PlayerMatchStats
    {
        public double Wins { get; set; }
        public int Losses { get; set; }
        public int Draws { get; set; }
        public int PointsFor { get; set; }
        public int PointsAgainst { get; set; }
        public HashSet<string> Opponents { get; set; } = new HashSet<string>();
        
        /// <summary>
        /// Total games played (Wins + Losses + Draws).
        /// Note: Draws add 0.5 to Wins, so this calculation accounts for that.
        /// </summary>
        public double TotalGames => Wins + Losses;
    }
    
    /// <summary>
    /// Complete Swiss system statistics for a tournament.
    /// </summary>
    public class SwissStats
    {
        /// <summary>
        /// Player statistics keyed by player ID.
        /// </summary>
        public Dictionary<string, PlayerMatchStats> PlayerStats { get; set; } = new Dictionary<string, PlayerMatchStats>();
        
        /// <summary>
        /// Sum of Opponents' Scores for each player.
        /// </summary>
        public Dictionary<string, double> SOS { get; set; } = new Dictionary<string, double>();
        
        /// <summary>
        /// Sum of Opponents' SOS for each player.
        /// </summary>
        public Dictionary<string, double> SOSOS { get; set; } = new Dictionary<string, double>();
    }
    
    /// <summary>
    /// Service for Swiss system tournament calculations.
    /// </summary>
    public interface ISwissSystemService
    {
        /// <summary>
        /// Calculate Swiss-system stats for tournament players.
        /// </summary>
        /// <param name="matches">Tournament matches.</param>
        /// <returns>Swiss statistics including SOS and SOSOS.</returns>
        SwissStats CalculateSwissStats(IEnumerable<Match> matches);
        
        /// <summary>
        /// Calculate positions using Swiss-system ranking.
        /// All undefeated players (0 losses, at least 1 win) get position 1 (champions).
        /// Other players keep their true positions.
        /// </summary>
        /// <param name="stats">Pre-calculated Swiss stats.</param>
        /// <returns>Dictionary of player ID to position.</returns>
        Dictionary<string, int> CalculateSwissPositions(SwissStats stats);
        
        /// <summary>
        /// Calculate Swiss positions directly from matches.
        /// </summary>
        /// <param name="matches">Tournament matches.</param>
        /// <returns>Dictionary of player ID to position.</returns>
        Dictionary<string, int> CalculateSwissPositions(IEnumerable<Match> matches);
    }
}

