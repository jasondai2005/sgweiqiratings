using System;
using System.Threading.Tasks;
using PlayerRatings.Models;

namespace PlayerRatings.Services.Leagues
{
    /// <summary>
    /// Service for league-related operations.
    /// </summary>
    public interface ILeagueService
    {
        /// <summary>
        /// Checks if the specified league is the Singapore Weiqi league.
        /// </summary>
        /// <param name="leagueName">The league name.</param>
        /// <returns>True if it's the SG league.</returns>
        bool IsSgLeague(string leagueName);
        
        /// <summary>
        /// Checks if the specified league is the Singapore Weiqi league.
        /// </summary>
        /// <param name="league">The league.</param>
        /// <returns>True if it's the SG league.</returns>
        bool IsSgLeague(Models.League league);
        
        /// <summary>
        /// Gets a league by ID.
        /// </summary>
        /// <param name="leagueId">The league ID.</param>
        /// <returns>The league, or null if not found.</returns>
        Task<Models.League> GetLeagueAsync(Guid leagueId);
        
        /// <summary>
        /// Gets a league by ID with authorization check.
        /// </summary>
        /// <param name="leagueId">The league ID.</param>
        /// <param name="userId">The user ID to check membership.</param>
        /// <returns>The league if user is a member, null otherwise.</returns>
        Task<Models.League> GetLeagueForMemberAsync(Guid leagueId, string userId);
        
        /// <summary>
        /// Gets a league by ID with admin authorization check.
        /// </summary>
        /// <param name="leagueId">The league ID.</param>
        /// <param name="userId">The user ID to check admin rights.</param>
        /// <returns>The league if user is admin, null otherwise.</returns>
        Task<Models.League> GetLeagueForAdminAsync(Guid leagueId, string userId);
        
        /// <summary>
        /// Checks if a user is a member of a league.
        /// </summary>
        /// <param name="leagueId">The league ID.</param>
        /// <param name="userId">The user ID.</param>
        /// <returns>True if user is a member.</returns>
        Task<bool> IsLeagueMemberAsync(Guid leagueId, string userId);
        
        /// <summary>
        /// Checks if a user is an admin of a league.
        /// </summary>
        /// <param name="leagueId">The league ID.</param>
        /// <param name="userId">The user ID.</param>
        /// <returns>True if user is an admin.</returns>
        Task<bool> IsLeagueAdminAsync(Guid leagueId, string userId);
    }
}

