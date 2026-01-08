using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PlayerRatings.Engine.Rating;
using PlayerRatings.Engine.Stats;
using PlayerRatings.Infrastructure.Caching;
using PlayerRatings.Models;

namespace PlayerRatings.Services.Rating
{
    /// <summary>
    /// Service for calculating and managing player ratings.
    /// Consolidates rating logic from LeaguesController and TournamentsController.
    /// </summary>
    public class RatingService : IRatingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;

        public RatingService(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        /// <inheritdoc />
        public League GetLeagueWithMatches(Guid leagueId, bool forceRefresh = false)
        {
            string cacheKey = CacheKeys.League(leagueId);

            if (forceRefresh)
            {
                _cache.Remove(cacheKey);
            }

            if (!_cache.TryGetValue(cacheKey, out League league))
            {
                league = _context.League
                    .Include(l => l.Matches).ThenInclude(m => m.FirstPlayer).ThenInclude(p => p.Rankings)
                    .Include(l => l.Matches).ThenInclude(m => m.SecondPlayer).ThenInclude(p => p.Rankings)
                    .Include(l => l.Matches).ThenInclude(m => m.Tournament)
                    .AsSplitQuery()
                    .SingleOrDefault(m => m.Id == leagueId);

                if (league != null)
                {
                    // Detach entities before caching so they're not tied to this DbContext
                    _context.ChangeTracker.Clear();
                    
                    _cache.Set(cacheKey, league, CacheDurations.League);
                }
            }

            return league;
        }

        /// <inheritdoc />
        public RatingResult CalculateRatings(Guid leagueId, RatingOptions options)
        {
            var league = GetLeagueWithMatches(leagueId, options.ForceRefresh);
            if (league == null)
            {
                return null;
            }

            // Try to get from cache
            string cacheKey = CacheKeys.LeagueRatings(leagueId, options.CutoffDate, options.SwaOnly);
            
            if (!options.ForceRefresh && _cache.TryGetValue(cacheKey, out RatingResult cachedResult))
            {
                return cachedResult;
            }

            var (eloStat, activeUsers) = CalculateRatingsFromMatches(league.Matches, options);

            var result = new RatingResult
            {
                EloStat = eloStat,
                ActiveUsers = activeUsers,
                League = league
            };

            _cache.Set(cacheKey, result, CacheDurations.Ratings);

            return result;
        }

        /// <inheritdoc />
        public (EloStat eloStat, HashSet<ApplicationUser> activeUsers) CalculateRatingsFromMatches(
            IEnumerable<Match> matches,
            RatingOptions options,
            Action<Match, EloStat> onMatchProcessed = null)
        {
            return RatingCalculationHelper.CalculateRatings(
                matches,
                options.CutoffDate,
                options.SwaOnly,
                options.IsSgLeague,
                options.AllowedUserIds,
                onMatchProcessed);
        }

        /// <inheritdoc />
        public Dictionary<string, PlayerRatingInfo> GetPlayerRatingsAtDate(
            IEnumerable<Match> matches,
            IEnumerable<string> playerIds,
            RatingOptions options)
        {
            var playerIdList = playerIds.ToList();
            
            // Build a lookup for players
            var allPlayers = matches
                .SelectMany(m => new[] { m.FirstPlayer, m.SecondPlayer })
                .Where(p => p != null)
                .Distinct()
                .ToDictionary(p => p.Id);

            var ratingsAndStatus = RatingCalculationHelper.GetPlayerRatingsAndRankedStatus(
                matches,
                options.CutoffDate,
                options.SwaOnly,
                options.IsSgLeague,
                playerIdList,
                id => allPlayers.TryGetValue(id, out var p) ? p : null);

            return ratingsAndStatus.ToDictionary(
                kvp => kvp.Key,
                kvp => new PlayerRatingInfo
                {
                    PlayerId = kvp.Key,
                    Rating = kvp.Value.rating,
                    IsRanked = kvp.Value.isRanked
                });
        }

        /// <inheritdoc />
        public Dictionary<string, double> GetPromotionBonuses(
            IEnumerable<Match> matches,
            IEnumerable<string> playerIds,
            DateTimeOffset startDate,
            DateTimeOffset endDate,
            RatingOptions options)
        {
            return RatingCalculationHelper.GetPromotionBonuses(
                matches,
                startDate,
                endDate,
                options.SwaOnly,
                options.IsSgLeague,
                playerIds);
        }

        /// <inheritdoc />
        public void InvalidateLeagueCache(Guid leagueId)
        {
            _cache.Remove(CacheKeys.League(leagueId));
            // Also remove any rating caches for this league
            // Note: IMemoryCache doesn't support wildcard removal, so we rely on expiration
        }
    }
}

