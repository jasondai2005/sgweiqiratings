using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PlayerRatings.Models;

namespace PlayerRatings.Services.Leagues
{
    /// <summary>
    /// Service for league-related operations.
    /// </summary>
    public class LeagueService : ILeagueService
    {
        private readonly ApplicationDbContext _context;
        
        /// <summary>
        /// The keyword that identifies the Singapore Weiqi league.
        /// </summary>
        private const string SG_LEAGUE_IDENTIFIER = "Singapore Weiqi";

        public LeagueService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public bool IsSgLeague(string leagueName)
        {
            return leagueName?.Contains(SG_LEAGUE_IDENTIFIER) ?? false;
        }

        /// <inheritdoc />
        public bool IsSgLeague(Models.League league)
        {
            return IsSgLeague(league?.Name);
        }

        /// <inheritdoc />
        public async Task<Models.League> GetLeagueAsync(Guid leagueId)
        {
            return await _context.League
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == leagueId);
        }

        /// <inheritdoc />
        public async Task<Models.League> GetLeagueForMemberAsync(Guid leagueId, string userId)
        {
            var isMember = await _context.LeaguePlayers
                .AnyAsync(lp => lp.LeagueId == leagueId && lp.UserId == userId);
            
            if (!isMember)
                return null;
            
            return await GetLeagueAsync(leagueId);
        }

        /// <inheritdoc />
        public async Task<Models.League> GetLeagueForAdminAsync(Guid leagueId, string userId)
        {
            return await _context.League
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == leagueId && l.CreatedByUserId == userId);
        }

        /// <inheritdoc />
        public async Task<bool> IsLeagueMemberAsync(Guid leagueId, string userId)
        {
            return await _context.LeaguePlayers
                .AnyAsync(lp => lp.LeagueId == leagueId && lp.UserId == userId);
        }

        /// <inheritdoc />
        public async Task<bool> IsLeagueAdminAsync(Guid leagueId, string userId)
        {
            return await _context.League
                .AnyAsync(l => l.Id == leagueId && l.CreatedByUserId == userId);
        }
    }
}

