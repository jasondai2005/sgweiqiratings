using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PlayerRatings.Engine.Rating;
using PlayerRatings.Infrastructure.Caching;
using PlayerRatings.Models;
using PlayerRatings.Services.Swiss;

namespace PlayerRatings.Services.Tournament
{
    /// <summary>
    /// Service for tournament-related operations.
    /// Extracted from TournamentsController for better reusability and testability.
    /// </summary>
    public class TournamentService : ITournamentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ISwissSystemService _swissService;

        public TournamentService(
            ApplicationDbContext context,
            IMemoryCache cache,
            ISwissSystemService swissService)
        {
            _context = context;
            _cache = cache;
            _swissService = swissService;
        }

        /// <inheritdoc />
        public async Task<Models.Tournament> GetTournamentAsync(Guid tournamentId, TournamentLoadOptions options = null)
        {
            options ??= new TournamentLoadOptions();
            
            string cacheKey = CacheKeys.Tournament(tournamentId);
            
            if (options.ForceRefresh)
            {
                _cache.Remove(cacheKey);
            }

            if (!_cache.TryGetValue(cacheKey, out Models.Tournament tournament))
            {
                var query = _context.Tournaments.AsQueryable();
                
                if (options.IncludeMatches)
                {
                    query = query
                        .Include(t => t.Matches).ThenInclude(m => m.FirstPlayer).ThenInclude(p => p.Rankings)
                        .Include(t => t.Matches).ThenInclude(m => m.SecondPlayer).ThenInclude(p => p.Rankings);
                }
                
                if (options.IncludePlayers)
                {
                    query = query
                        .Include(t => t.TournamentPlayers).ThenInclude(tp => tp.Player).ThenInclude(p => p.Rankings)
                        .Include(t => t.League);
                }
                
                tournament = await query
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(t => t.Id == tournamentId);

                if (tournament != null)
                {
                    _context.ChangeTracker.Clear();
                    _cache.Set(cacheKey, tournament, CacheDurations.Tournament);
                }
            }

            return tournament;
        }

        /// <inheritdoc />
        public bool IsSgLeague(string leagueName)
        {
            return leagueName?.Contains("Singapore Weiqi") ?? false;
        }

        /// <inheritdoc />
        public TournamentRatingDelta CalculateRatingDeltas(
            Models.Tournament tournament,
            IEnumerable<Match> leagueMatches,
            bool isSgLeague,
            bool swaOnly)
        {
            var result = new TournamentRatingDelta();
            
            if (!tournament.Matches.Any())
            {
                return result;
            }

            // "Before" cutoff: 1 second before the earliest tournament match (so tournament matches are excluded)
            // "After" cutoff: the latest tournament match time (so all tournament matches are included)
            var hasMatches = tournament.Matches.Any();
            var earliestMatchTime = hasMatches 
                ? tournament.Matches.Min(m => m.Date) 
                : (tournament.StartDate ?? DateTimeOffset.UtcNow);
            var latestMatchTime = hasMatches 
                ? tournament.Matches.Max(m => m.Date) 
                : (tournament.EndDate ?? DateTimeOffset.UtcNow);
            
            var beforeCutoff = earliestMatchTime.AddSeconds(-1);
            var afterCutoff = latestMatchTime;
            
            // Get tournament player IDs
            var tournamentPlayerIds = tournament.TournamentPlayers.Select(tp => tp.PlayerId).ToHashSet();
            
            // Build player lookup from all sources
            var allPlayers = BuildPlayerLookup(tournament, leagueMatches);
            ApplicationUser PlayerLookup(string playerId) => 
                allPlayers.TryGetValue(playerId, out var player) ? player : null;

            // Calculate ratings before and after
            var ratingsBefore = RatingCalculationHelper.GetPlayerRatingsAndRankedStatus(
                leagueMatches, beforeCutoff, swaOnly, isSgLeague, tournamentPlayerIds, PlayerLookup);
            
            var ratingsAfter = RatingCalculationHelper.GetPlayerRatingsAndRankedStatus(
                leagueMatches, afterCutoff, swaOnly, isSgLeague, tournamentPlayerIds, PlayerLookup);
            
            // Get promotion bonuses
            var promotionBonuses = RatingCalculationHelper.GetPromotionBonuses(
                leagueMatches, beforeCutoff, afterCutoff, swaOnly, isSgLeague, tournamentPlayerIds);

            // Convert to simple dictionaries
            foreach (var kvp in ratingsBefore)
            {
                result.RatingsBefore[kvp.Key] = kvp.Value.rating;
            }
            
            foreach (var kvp in ratingsAfter)
            {
                result.RatingsAfter[kvp.Key] = kvp.Value.rating;
            }
            
            result.PromotionBonuses = promotionBonuses;
            
            return result;
        }

        /// <inheritdoc />
        public SwissStats CalculateSwissStandings(IEnumerable<Match> matches)
        {
            return _swissService.CalculateSwissStats(matches);
        }

        /// <inheritdoc />
        public string GetTournamentFullName(Models.Tournament tournament)
        {
            if (tournament == null)
                return string.Empty;
                
            var parts = new List<string>();
            
            if (!string.IsNullOrEmpty(tournament.Ordinal))
                parts.Add(tournament.Ordinal);
            
            if (!string.IsNullOrEmpty(tournament.Name))
                parts.Add(tournament.Name);
            
            if (!string.IsNullOrEmpty(tournament.Group) && tournament.TournamentType != Models.Tournament.TypeTitle)
                parts.Add($"({tournament.Group})");
            
            return string.Join(" ", parts);
        }

        /// <inheritdoc />
        public void InvalidateTournamentCache(Guid tournamentId)
        {
            _cache.Remove(CacheKeys.Tournament(tournamentId));
            _cache.Remove(CacheKeys.TournamentSwiss(tournamentId));
        }

        private Dictionary<string, ApplicationUser> BuildPlayerLookup(
            Models.Tournament tournament,
            IEnumerable<Match> matches)
        {
            var allPlayers = new Dictionary<string, ApplicationUser>();
            
            // Add from tournament players
            if (tournament.TournamentPlayers != null)
            {
                foreach (var tp in tournament.TournamentPlayers)
                {
                    if (tp.Player != null && !allPlayers.ContainsKey(tp.PlayerId))
                        allPlayers[tp.PlayerId] = tp.Player;
                }
            }
            
            // Add from matches
            foreach (var match in matches)
            {
                if (match.FirstPlayer != null && !allPlayers.ContainsKey(match.FirstPlayerId))
                    allPlayers[match.FirstPlayerId] = match.FirstPlayer;
                if (match.SecondPlayer != null && !allPlayers.ContainsKey(match.SecondPlayerId))
                    allPlayers[match.SecondPlayerId] = match.SecondPlayer;
            }
            
            return allPlayers;
        }
    }
}

