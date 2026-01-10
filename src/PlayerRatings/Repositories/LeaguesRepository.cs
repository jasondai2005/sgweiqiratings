using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PlayerRatings.Models;

namespace PlayerRatings.Repositories
{
    public class LeaguesRepository : ILeaguesRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LeaguesRepository(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }
        
        // Request-scoped cache key for admin authorization
        private string GetAdminCacheKey(string userId, Guid leagueId) => $"AdminAuth_{userId}_{leagueId}";

        public IEnumerable<League> GetLeagues(ApplicationUser user)
        {
            return _context.LeaguePlayers
                .AsNoTracking()
                .Include(lp => lp.League)
                .Where(lp => lp.UserId == user.Id)
                .Select(lp => lp.League)
                .Distinct()
                .ToList();
        }

        public League GetUserAuthorizedLeague(ApplicationUser user, Guid leagueId)
        {
            var league = _context.League.Single(m => m.Id == leagueId);
            if (league == null)
            {
                return null;
            }

            return _context.LeaguePlayers.Any(lp => lp.LeagueId == leagueId && lp.UserId == user.Id) ? league : null;
        }

        public League GetAdminAuthorizedLeague(ApplicationUser user, Guid leagueId)
        {
            var httpContext = _httpContextAccessor?.HttpContext;
            if (httpContext == null)
            {
                // No HTTP context (e.g., in tests), query directly
                return _context.League.SingleOrDefault(m => m.Id == leagueId && m.CreatedByUserId == user.Id);
            }
            
            var cacheKey = GetAdminCacheKey(user.Id, leagueId);
            
            // Check request-scoped cache
            if (httpContext.Items.TryGetValue(cacheKey, out var cached))
            {
                return cached as League;
            }
            
            // Query and cache for this request
            var league = _context.League.SingleOrDefault(m => m.Id == leagueId && m.CreatedByUserId == user.Id);
            httpContext.Items[cacheKey] = league;
            return league;
        }

        public IEnumerable<League> GetAdminAuthorizedLeagues(ApplicationUser user)
        {
            return _context.League
                .AsNoTracking()
                .Where(l => l.CreatedByUserId == user.Id)
                .ToList();
        }
    }
}
