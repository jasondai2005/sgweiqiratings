using System;
using System.Collections.Generic;
using System.Linq;
using PlayerRatings.Engine.Stats;
using PlayerRatings.Models;

namespace PlayerRatings.Engine.Rating
{
    /// <summary>
    /// Shared helper methods for rating calculations used by multiple controllers.
    /// </summary>
    public static class RatingCalculationHelper
    {
        // Match type constants
        public const string MATCH_SWA = "SWA ";

        // Minimum date for rating calculations (matches before this date are not included in ratings)
        public static readonly DateTimeOffset RATING_START_DATE = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// Resets player transient state before rating calculation.
        /// This is important when using cached data to ensure each calculation starts fresh.
        /// </summary>
        public static void ResetPlayerState(IEnumerable<Match> matches)
        {
            var resetPlayers = new HashSet<string>();
            foreach (var match in matches)
            {
                if (match.FirstPlayer != null && resetPlayers.Add(match.FirstPlayerId))
                {
                    ResetPlayer(match.FirstPlayer);
                }
                if (match.SecondPlayer != null && resetPlayers.Add(match.SecondPlayerId))
                {
                    ResetPlayer(match.SecondPlayer);
                }
            }
        }

        /// <summary>
        /// Resets a single player's transient state.
        /// </summary>
        public static void ResetPlayer(ApplicationUser player)
        {
            player.MatchCount = 0;
            player.FirstMatch = DateTimeOffset.MinValue;
            player.LastMatch = DateTimeOffset.MinValue;
            player.PreviousMatchDate = DateTimeOffset.MinValue;
            player.MatchesSinceReturn = 0;
            player.EstimatedInitialRating = null;
        }

        /// <summary>
        /// Updates player state when processing a match and adds to active users set.
        /// </summary>
        public static void AddUser(HashSet<ApplicationUser> activeUsers, Match match, ApplicationUser player)
        {
            if (player.FirstMatch == DateTimeOffset.MinValue)
            {
                player.FirstMatch = match.Date;
            }

            // Check for 2+ year gap (returning inactive player)
            if (player.LastMatch != DateTimeOffset.MinValue)
            {
                var gap = match.Date - player.LastMatch;
                if (gap.TotalDays > 365 * 2)
                {
                    // Detected a 2+ year gap, reset the counter
                    player.MatchesSinceReturn = 1;
                }
                else if (player.MatchesSinceReturn > 0)
                {
                    // Already tracking since return, increment counter
                    player.MatchesSinceReturn++;
                }
            }

            player.PreviousMatchDate = player.LastMatch;
            player.LastMatch = match.Date;
            player.MatchCount++;

            activeUsers.Add(player);
        }

        /// <summary>
        /// Applies SWA filter and ordering to pre-filtered matches.
        /// For non-SG leagues (international), all match types are included.
        /// For SG leagues with swaOnly=true, only SWA matches are included.
        /// </summary>
        /// <param name="matches">Pre-filtered matches to apply SWA filter to</param>
        /// <param name="swaOnly">Filter for SWA tournaments only</param>
        /// <param name="isSgLeague">If true, may filter by SWA. If false, includes all match types.</param>
        public static IOrderedEnumerable<Match> ApplySwaFilter(IEnumerable<Match> matches, bool swaOnly, bool isSgLeague = true)
        {
            // Non-SG (international) leagues include all match types
            if (!isSgLeague || !swaOnly)
                return matches.OrderBy(m => m.Date);
            
            // Match filter includes SWA matches by Tournament Organizer OR by MatchName
            return matches
                .Where(m => (m.Tournament?.Organizer?.Contains(MATCH_SWA.Trim()) ?? false) || (m.MatchName?.Contains(MATCH_SWA) ?? false))
                .OrderBy(m => m.Date);
        }

        /// <summary>
        /// Filters matches based on date and match type for rating calculations.
        /// Excludes matches before RATING_START_DATE (01/01/2023).
        /// For non-SG leagues (international), all match types are included.
        /// </summary>
        /// <param name="matches">All matches to filter</param>
        /// <param name="cutoffDate">Only include matches up to this date</param>
        /// <param name="swaOnly">Filter for SWA tournaments only</param>
        /// <param name="isSgLeague">If true, filters by match type (SWA/TGA/SG). If false, includes all match types.</param>
        public static IOrderedEnumerable<Match> FilterMatches(IEnumerable<Match> matches, DateTimeOffset cutoffDate, bool swaOnly, bool isSgLeague = true)
        {
            // Date filter: between RATING_START_DATE and cutoff date
            var dateFiltered = matches.Where(x => x.Date >= RATING_START_DATE && x.Date <= cutoffDate);
            return ApplySwaFilter(dateFiltered, swaOnly, isSgLeague);
        }

        /// <summary>
        /// Full rating calculation with all filters and corrections.
        /// Matches the behavior of LeaguesController.CalculateRatings.
        /// </summary>
        /// <param name="allMatches">All matches to process</param>
        /// <param name="cutoffDate">Date to calculate ratings up to</param>
        /// <param name="swaOnly">Filter for SWA tournaments only</param>
        /// <param name="isSgLeague">Whether this is the Singapore Weiqi league</param>
        /// <param name="allowedUserIds">Optional filter - only include these user IDs (null = include all)</param>
        /// <param name="onMatchProcessed">Optional callback for each match with EloStat (for monthly snapshots)</param>
        /// <returns>Tuple of (EloStat with calculated ratings, activeUsers set)</returns>
        public static (EloStat eloStat, HashSet<ApplicationUser> activeUsers) CalculateRatings(
            IEnumerable<Match> allMatches,
            DateTimeOffset cutoffDate,
            bool swaOnly,
            bool isSgLeague,
            HashSet<string> allowedUserIds = null,
            Action<Match, EloStat> onMatchProcessed = null)
        {
            // Reset player transient state at the start
            ResetPlayerState(allMatches);

            League.CutoffDate = cutoffDate;
            EloStat.SwaOnly = swaOnly;

            var activeUsers = new HashSet<ApplicationUser>();
            var eloStat = new EloStat();

            var matches = FilterMatches(allMatches, cutoffDate, swaOnly, isSgLeague);
            foreach (var match in matches)
            {
                // Skip rating calculation for Factor=0 matches (byes, unrated games)
                if (match.Factor != 0)
                {
                    // Add users (with optional filter) - skip NULL players for bye matches
                    if (match.FirstPlayer != null && (allowedUserIds == null || allowedUserIds.Contains(match.FirstPlayerId)))
                    {
                        AddUser(activeUsers, match, match.FirstPlayer);
                    }
                    if (match.SecondPlayer != null && (allowedUserIds == null || allowedUserIds.Contains(match.SecondPlayerId)))
                    {
                        AddUser(activeUsers, match, match.SecondPlayer);
                    }

                    eloStat.AddMatch(match);
                }
                else if (match.SecondPlayer == null || match.FirstPlayerScore == 0)
                {
                    continue; // Bye match for FirstPlayer, skip
                }

                onMatchProcessed?.Invoke(match, eloStat);
            }

            // Check promotions for all active users
            foreach (var user in activeUsers)
            {
                eloStat.CheckPlayerPromotion(user, cutoffDate, isSgLeague);
            }

            return (eloStat, activeUsers);
        }

        /// <summary>
        /// Gets player ratings at a specific date from a list of matches.
        /// Uses full rating calculation with all filters and corrections.
        /// </summary>
        /// <param name="allMatches">All matches ordered by date</param>
        /// <param name="cutoffDate">Calculate ratings up to this date</param>
        /// <param name="swaOnly">Filter for SWA tournaments only</param>
        /// <param name="isSgLeague">Whether this is the Singapore Weiqi league</param>
        /// <param name="playerIds">Player IDs to get ratings for</param>
        /// <param name="playerLookup">Function to lookup player by ID</param>
        /// <returns>Dictionary of player ID to rating</returns>
        public static Dictionary<string, int> GetPlayerRatingsAtDate(
            IEnumerable<Match> allMatches,
            DateTimeOffset cutoffDate,
            bool swaOnly,
            bool isSgLeague,
            IEnumerable<string> playerIds,
            Func<string, ApplicationUser> playerLookup)
        {
            var ratings = new Dictionary<string, int>();
            
            // Calculate ratings with all filters and corrections
            var (eloStat, _) = CalculateRatings(allMatches, cutoffDate, swaOnly, isSgLeague);
            
            // Get ratings for specified players
            foreach (var playerId in playerIds)
            {
                var player = playerLookup(playerId);
                if (player != null)
                {
                    ratings[playerId] = (int)Math.Round(eloStat[player]);
                }
            }
            
            return ratings;
        }

        /// <summary>
        /// Gets player ratings and ranked status at a specific date.
        /// A player is "ranked" if they would appear in the Ratings page at that date:
        /// - Has played matches (is in activeUsers)
        /// - Is active (played within 2 years of cutoff date)
        /// - Not a hidden player (for SG league)
        /// - Is local player (for SG league)
        /// </summary>
        /// <param name="allMatches">All matches ordered by date</param>
        /// <param name="cutoffDate">Calculate ratings up to this date</param>
        /// <param name="swaOnly">Filter for SWA tournaments only</param>
        /// <param name="isSgLeague">Whether this is the Singapore Weiqi league</param>
        /// <param name="playerIds">Player IDs to get ratings for</param>
        /// <param name="playerLookup">Function to lookup player by ID</param>
        /// <returns>Dictionary of player ID to (rating, isRanked)</returns>
        public static Dictionary<string, (double rating, bool isRanked)> GetPlayerRatingsAndRankedStatus(
            IEnumerable<Match> allMatches,
            DateTimeOffset cutoffDate,
            bool swaOnly,
            bool isSgLeague,
            IEnumerable<string> playerIds,
            Func<string, ApplicationUser> playerLookup)
        {
            var results = new Dictionary<string, (double rating, bool isRanked)>();
            
            // Calculate ratings with all filters and corrections
            var (eloStat, activeUsers) = CalculateRatings(allMatches, cutoffDate, swaOnly, isSgLeague);
            
            // Build set of active user IDs for quick lookup
            var activeUserIds = new HashSet<string>(activeUsers.Select(u => u.Id));
            
            // Calculate the "active" threshold (2 years before cutoff)
            var twoYearsAgo = cutoffDate.AddYears(-2);
            
            // Get ratings and ranked status for specified players
            foreach (var playerId in playerIds)
            {
                var player = playerLookup(playerId);
                if (player != null)
                {
                    double rating = eloStat[player];
                    
                    // Check if player would be "ranked" (shown in ratings page) at this date
                    // 1. Must be in activeUsers (has played matches)
                    bool isInActiveUsers = activeUserIds.Contains(playerId);
                    
                    // Only check further conditions if player is in activeUsers
                    // This ensures FirstMatch has been set (required for IsHiddenPlayer check)
                    bool isRanked = false;
                    if (isInActiveUsers)
                    {
                        // 2. Must be active (LastMatch within 2 years) OR be a pro player
                        bool isActive = player.LastMatch > twoYearsAgo || player.IsProPlayer;
                        
                        // 3. For SG league: must not be hidden, must be local
                        // IsHiddenPlayer requires FirstMatch to be set, which is guaranteed since player is in activeUsers
                        bool isVisibleInSgLeague = !isSgLeague || (!player.IsHiddenPlayer && player.IsLocalPlayerAt(cutoffDate));
                        
                        isRanked = isActive && isVisibleInSgLeague;
                    }
                    
                    results[playerId] = (rating, isRanked);
                }
            }
            
            return results;
        }

        /// <summary>
        /// Gets promotion bonus for a player during a date range.
        /// Returns the sum of all promotion bonuses awarded between startDate and endDate.
        /// </summary>
        /// <param name="allMatches">All matches ordered by date</param>
        /// <param name="startDate">Start of date range</param>
        /// <param name="endDate">End of date range</param>
        /// <param name="swaOnly">Filter for SWA tournaments only</param>
        /// <param name="isSgLeague">Whether this is the Singapore Weiqi league</param>
        /// <param name="playerIds">Player IDs to get bonuses for</param>
        /// <returns>Dictionary of player ID to total promotion bonus amount</returns>
        public static Dictionary<string, double> GetPromotionBonuses(
            IEnumerable<Match> allMatches,
            DateTimeOffset startDate,
            DateTimeOffset endDate,
            bool swaOnly,
            bool isSgLeague,
            IEnumerable<string> playerIds)
        {
            var results = new Dictionary<string, double>();
            
            // Calculate ratings up to end date to capture all promotions
            var (eloStat, _) = CalculateRatings(allMatches, endDate, swaOnly, isSgLeague);
            
            // Get promotion bonuses for each player that occurred between start and end dates
            foreach (var playerId in playerIds)
            {
                // Get all bonuses up to end date (this consumes them from the tracker)
                var bonuses = eloStat.ConsumePromotionBonuses(playerId, endDate);
                
                // Sum bonuses that occurred after start date
                var totalBonus = bonuses
                    .Where(b => b.promotionDate.HasValue && b.promotionDate.Value >= startDate)
                    .Sum(b => b.bonusAmount);
                
                if (totalBonus > 0)
                {
                    results[playerId] = totalBonus;
                }
            }
            
            return results;
        }
    }
}

