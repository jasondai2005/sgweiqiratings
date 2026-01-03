using Microsoft.AspNetCore.Identity;
using PlayerRatings.Engine.Rating;

namespace PlayerRatings.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        internal const string DATE_FORMAT = "dd/MM/yyyy";
        private Dictionary<string, DateTimeOffset> _rankingHistory = null;
        private Dictionary<string, DateTimeOffset> _swaRankingHistory = null;


        public string DisplayName { get; set; }

        /// <summary>
        /// Player's birth year (new column, replaces BY: in Ranking string)
        /// </summary>
        public int? BirthYearValue { get; set; }

        /// <summary>
        /// Player's residence history. Format: "{Place} ({year}); {Place} ({year}); {Place}"
        /// Example: "Singapore (2020); Malaysia (2018); China"
        /// When year is not supplied, player is considered to have lived there since min date.
        /// Otherwise, since the beginning of that year.
        /// </summary>
        public string Residence { get; set; }

        /// <summary>
        /// Path or URL to player's photo
        /// </summary>
        public string Photo { get; set; }

        /// <summary>
        /// Navigation property to player's ranking history
        /// </summary>
        public virtual ICollection<PlayerRanking> Rankings { get; set; }

        /// <summary>
        /// Gets birth year as string
        /// </summary>
        public string BirthYear => BirthYearValue?.ToString() ?? string.Empty;

        /// <summary>
        /// Gets birth year only if player is under 18
        /// </summary>
        public string BirthYearU18 => BirthYearValue.HasValue && BirthYearValue.Value + 18 >= DateTime.Now.Year 
            ? BirthYearValue.Value.ToString() 
            : string.Empty;

        /// <summary>
        /// Gets the current residence (first entry in residence history, without year).
        /// Used for display purposes.
        /// </summary>
        public string CurrentResidence => GetResidenceAt(DateTimeOffset.Now);

        /// <summary>
        /// Gets the residence at a specific date.
        /// Parses residence history format: "{Place} ({year}); {Place} ({year}); {Place}"
        /// Returns the place name without the year.
        /// </summary>
        public string GetResidenceAt(DateTimeOffset date)
            {
                if (string.IsNullOrEmpty(Residence))
                    return string.Empty;
                
            // Parse residence history entries (most recent first)
            var entries = Residence.Split(';')
                .Select(e => e.Trim())
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();

            foreach (var entry in entries)
            {
                // Parse place and optional year: "Singapore (2020)" or "Singapore"
                var place = entry;
                int? year = null;

                var parenIndex = entry.LastIndexOf('(');
                if (parenIndex > 0 && entry.EndsWith(")"))
                {
                    place = entry.Substring(0, parenIndex).Trim();
                    var yearStr = entry.Substring(parenIndex + 1, entry.Length - parenIndex - 2);
                    if (int.TryParse(yearStr, out int parsedYear))
                        year = parsedYear;
                }

                // Check if this entry applies to the given date
                // If year is specified, this residence is valid from beginning of that year
                // If no year, this is the earliest known residence (valid from min date)
                var entryStartDate = year.HasValue 
                    ? new DateTimeOffset(year.Value, 1, 1, 0, 0, 0, TimeSpan.Zero)
                    : DateTimeOffset.MinValue;

                if (date >= entryStartDate)
                {
                    return place;
                }
            }

            // If no entry matches, use the last (oldest) entry
            if (entries.Any())
            {
                var lastEntry = entries.Last();
                var parenIndex = lastEntry.LastIndexOf('(');
                return parenIndex > 0 ? lastEntry.Substring(0, parenIndex).Trim() : lastEntry;
            }

            return string.Empty;
        }

        /// <summary>
        /// Determines if this is a local player (lives in Singapore) at the current date.
        /// </summary>
        public bool IsLocalPlayer => IsLocalPlayerAt(DateTimeOffset.Now);

        /// <summary>
        /// Determines if this player lived in Singapore at the specified date.
        /// Parses residence history format: "{Place} ({year}); {Place} ({year}); {Place}"
        /// </summary>
        public bool IsLocalPlayerAt(DateTimeOffset date)
        {
            if (string.IsNullOrEmpty(Residence))
                return true; // Default to local if not specified

            // Parse residence history entries (most recent first)
            var entries = Residence.Split(';')
                .Select(e => e.Trim())
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();

            foreach (var entry in entries)
            {
                // Parse place and optional year: "Singapore (2020)" or "Singapore"
                var place = entry;
                int? year = null;

                var parenIndex = entry.LastIndexOf('(');
                if (parenIndex > 0 && entry.EndsWith(")"))
                {
                    place = entry.Substring(0, parenIndex).Trim();
                    var yearStr = entry.Substring(parenIndex + 1, entry.Length - parenIndex - 2);
                    if (int.TryParse(yearStr, out int parsedYear))
                        year = parsedYear;
                }

                // Check if this entry applies to the given date
                // If year is specified, this residence is valid from beginning of that year
                // If no year, this is the earliest known residence (valid from min date)
                var entryStartDate = year.HasValue 
                    ? new DateTimeOffset(year.Value, 1, 1, 0, 0, 0, TimeSpan.Zero)
                    : DateTimeOffset.MinValue;

                if (date >= entryStartDate)
                {
                    return place.Equals("Singapore", StringComparison.OrdinalIgnoreCase);
                }
            }

            // If no entry matches, use the last (oldest) entry
            if (entries.Any())
            {
                var lastEntry = entries.Last();
                var parenIndex = lastEntry.LastIndexOf('(');
                var place = parenIndex > 0 ? lastEntry.Substring(0, parenIndex).Trim() : lastEntry;
                return place.Equals("Singapore", StringComparison.OrdinalIgnoreCase);
            }

            return true; // Default to local
        }

        internal DateTimeOffset LastMatch { get; set; } = DateTimeOffset.MinValue;
        internal DateTimeOffset FirstMatch { get; set; } = DateTimeOffset.MinValue;

        internal int MatchCount { get; set; } = 0;

        /// <summary>
        /// Tracks the previous match date before updating LastMatch.
        /// Used to detect gaps in playing activity.
        /// </summary>
        internal DateTimeOffset PreviousMatchDate { get; set; } = DateTimeOffset.MinValue;

        /// <summary>
        /// Counts matches played since returning from a 2+ year break.
        /// Reset when a gap is detected.
        /// </summary>
        internal int MatchesSinceReturn { get; set; } = 0;

        /// <summary>
        /// Estimated initial rating calculated from performance in first 12 games.
        /// This is used to provide a more accurate initial rating for foreign/unknown players
        /// after they have played enough games to determine their true strength.
        /// </summary>
        internal double? EstimatedInitialRating { get; set; } = null;

        /// <summary>
        /// Gets the best available initial rating for this player.
        /// Returns the performance-based estimated rating if available (after 12 games),
        /// otherwise returns the ranking-based rating.
        /// </summary>
        public double GetInitialRating(bool intl = false)
        {
            // If we have a performance-based estimate (calculated after 12 games), use it
            if (EstimatedInitialRating.HasValue)
                return EstimatedInitialRating.Value;
            
            // Otherwise fall back to ranking-based rating
            return GetRatingBeforeDate(FirstMatch.Date, intl);
        }

        public bool Active
        {
            get
            {
                return League.CutoffDate.AddYears(-2) < LastMatch;
            }
        }

        private string m_initRanking = null;
        internal string InitRanking
        {
            get
            {
                if (m_initRanking == null)
                {
                    // Use combined ranking to show effective ranking (trusted kyu can override SWA kyu)
                    m_initRanking = GetCombinedRankingBeforeDate(FirstMatch.Date);
                }

                return m_initRanking;
            }
        }

        public bool IsUnknownPlayer => string.IsNullOrEmpty(InitRanking) || InitRanking.Contains("K?");
        
        /// <summary>
        /// Determines if this is a player with an unknown/foreign ranking at the time of their first match.
        /// Foreign rankings are indicated by IsForeignRanking in the PlayerRanking structure.
        /// Kyu players from trusted organizations (SWA, TGA, MWA, KBA, Thailand, Vietnam, EGF) can enter directly.
        /// </summary>
        public bool IsUnknownRankedPlayer
        {
            get
            {
                if (IsUnknownPlayer)
                    return true;

                // Get the ranking at the time of first match (or earliest if no match yet)
                GetCombinedRankingBeforeDate(FirstMatch, out PlayerRanking initialRanking);

                if (initialRanking == null)
                    return false;
                
                // Pro players are never unknown
                if (initialRanking.Ranking.Contains("P", StringComparison.OrdinalIgnoreCase))
                    return false;
                
                // Kyu players from trusted organizations can enter directly without estimation
                bool isKyu = initialRanking.Ranking.Contains("K", StringComparison.OrdinalIgnoreCase);
                if (isKyu && initialRanking.IsTrustedOrganization)
                    return false;
                
                // Foreign dan or untrusted rankings need performance estimation
                return initialRanking.IsForeignRanking;
            }
        }
        
        public bool IsNewUnknownRankdedPlayer => MatchCount <= 12 && IsUnknownRankedPlayer;
        
        public bool IsProPlayer
        {
            get
            {
                var ranking = RankingBeforeCutoffDate ?? string.Empty;
                return ranking.Contains('P', StringComparison.OrdinalIgnoreCase);
            }
        }
        
        public bool IsNewKyuPlayer
        {
            get
            {
                if (MatchCount > 12 || IsProPlayer)
                    return false;
                    
                var ranking = InitRanking ?? string.Empty;
                return !ranking.Contains('D', StringComparison.OrdinalIgnoreCase);
            }
        }
        
        /// <summary>
        /// Players in monitoring period (new kyu or unknown ranked players).
        /// Note: PlayerStatus.Hidden in LeaguePlayer handles admin-hidden players separately.
        /// </summary>
        public bool IsHiddenPlayer => IsNewKyuPlayer || IsNewUnknownRankdedPlayer;

        // Number of games to monitor for local dan players and returning players
        private const int LOCAL_PLAYER_GAMES_THRESHOLD = 6;

        /// <summary>
        /// Determines if a PlayerRanking is from a local organization (SWA/TGA).
        /// </summary>
        private static bool IsLocalRanking(PlayerRanking playerRanking)
        {
            if (playerRanking == null)
                return false;
            return playerRanking.IsLocalRanking;
        }

        /// <summary>
        /// New local dan player who started playing from 2025.
        /// These players have a local dan ranking (SWA like "2D" or TGA like "(2D)") but are new to the league.
        /// Foreign dan players with square brackets "[2D]" are handled separately by IsUnknownRankedPlayer.
        /// Only monitored for 6 games since their local ranking is more reliable.
        /// </summary>
        public bool IsNewLocalDanPlayer
        {
            get
            {
                if (MatchCount > LOCAL_PLAYER_GAMES_THRESHOLD || IsProPlayer)
                    return false;
                
                // Must have first match in 2025 or later
                if (FirstMatch == DateTimeOffset.MinValue || FirstMatch.Year < 2025)
                    return false;
                
                GetCombinedRankingBeforeDate(FirstMatch, out PlayerRanking initialRanking);
                if (initialRanking == null)
                    return false;

                bool isDan = initialRanking.Ranking?.Contains('D', StringComparison.OrdinalIgnoreCase) ?? false;
                
                return isDan && IsLocalRanking(initialRanking);
            }
        }

        /// <summary>
        /// Inactive player who has returned after a long break (2+ years).
        /// Their rating may no longer reflect their current strength.
        /// Only monitored for 6 games since they have prior playing history.
        /// </summary>
        public bool IsReturningInactivePlayer
        {
            get
            {
                // Check if we detected a 2+ year gap (MatchesSinceReturn > 0 means gap was detected)
                // and they haven't played enough games since returning
                return MatchesSinceReturn > 0 && MatchesSinceReturn <= LOCAL_PLAYER_GAMES_THRESHOLD;
            }
        }

        public bool NeedDynamicFactor(bool intl)
        {
            if (intl)
                return MatchCount <= 12;
            
            // Need dynamic factor for:
            // 1. New players with unknown/foreign ranking
            // 2. New local dan players (SWA/TGA) who started in 2025
            // 3. Inactive players returning after 2+ years
            return IsNewUnknownRankdedPlayer || IsNewLocalDanPlayer || IsReturningInactivePlayer;
        }

        /// <summary>
        /// Gets the latest ranking in combined format showing SWA ranking and highest other ranking.
        /// Examples: "1D (2D)" for SWA 1D + TGA 2D, "3D [5D]" for SWA 3D + foreign 5D
        /// </summary>
        public string LatestRanking => GetCombinedRankingBeforeDate(DateTimeOffset.Now.AddDays(1));

        public string LatestRankedDate
        {
            get
            {
                if (!RankingHistory.Any())
                    return string.Empty;
                var date = RankingHistory.First().Value;
                return date == DateTimeOffset.MinValue ? string.Empty : date.ToString(DATE_FORMAT);
            }
        }

        private string m_swaRanking, m_swaRankedDate;
        public string LatestSwaRanking
        {
            get
            {
                if (m_swaRanking == null)
                    GetSwaRanking(out m_swaRanking, out m_swaRankedDate);
                return m_swaRanking;
            }
        }
        public string LatestSwaRankedDate
        {
            get
            {
                if (m_swaRankedDate == null)
                    GetSwaRanking(out m_swaRanking, out m_swaRankedDate);
                return m_swaRankedDate;
            }
        }

        public string RankingBeforeCutoffDate
        {
            get
            {
                return GetCombinedRankingBeforeDate(League.CutoffDate);
            }
        }

        /// <summary>
        /// Gets combined ranking string before a specific date.
        /// For dan rankings: shows other ranking if >= SWA (equal means higher rank due to foreign dan penalty)
        /// For kyu rankings: shows other ranking only if > SWA (foreign kyu has same rating as SWA kyu)
        /// Examples: "1D (2D)" for SWA 1D + TGA 2D, "3K" for SWA 3K + TGA 3K (not shown because equal)
        /// </summary>
        public string GetCombinedRankingBeforeDate(DateTimeOffset date)
        {
            return GetCombinedRankingBeforeDate(date, out _);
        }

        public string GetCombinedRankingBeforeDate(DateTimeOffset date, out PlayerRanking effectiveRanking)
        {
            effectiveRanking = null;
            if (Rankings == null || !Rankings.Any())
                return string.Empty;

            // Helper: RankingDate is treated as end of that day (23:59:59)
            // A ranking dated Jan 15 takes effect at Jan 15 23:59:59
            bool IsRankingEffective(PlayerRanking r) => 
                r.RankingDate == null || r.RankingDate.Value.Date.AddDays(1).AddSeconds(-1) <= date;

            // Get latest SWA ranking effective at date
            var latestSwa = Rankings
                .Where(r => r.Organization == "SWA" && !string.IsNullOrEmpty(r.Ranking))
                .Where(IsRankingEffective)
                .OrderByDescending(r => r.RankingDate ?? DateTimeOffset.MinValue)
                .ThenByDescending(r => GetRatingByRanking(r))
                .FirstOrDefault();
            effectiveRanking = latestSwa;

            // Get highest ranking from any other organization effective at date
            var highestOther = Rankings
                .Where(r => r.Organization != "SWA" && !string.IsNullOrEmpty(r.Ranking))
                .Where(IsRankingEffective)
                .OrderByDescending(r => GetRatingByRanking(r))
                .ThenByDescending(r => r.RankingDate ?? DateTimeOffset.MinValue)
                .FirstOrDefault();

            // Build result
            string result = latestSwa?.Ranking ?? string.Empty;
            int swaRating = latestSwa != null ? GetRatingByRanking(latestSwa) : 0;
            int otherRating = highestOther != null ? GetRatingByRanking(highestOther) : 0;

            // For dan rankings: show other if >= (equal rating means higher rank due to foreign dan penalty)
            // For kyu rankings: show only if > (SWA takes priority when equal, foreign kyu has same rating)
            bool isKyuRanking = result.Contains("K", StringComparison.OrdinalIgnoreCase);
            bool shouldShowOther = isKyuRanking ? (otherRating > swaRating) : (otherRating >= swaRating);
            
            if (highestOther != null && shouldShowOther)
            {
                string otherFormatted = FormatRankingForDisplay(highestOther);
                if (!string.IsNullOrEmpty(result))
                {
                    // Trusted org kyu can replace SWA kyu (same as Pro and local rankings)
                    if (highestOther.Ranking.Contains("P") || (result.Contains("K") && highestOther.IsTrustedOrganization))
                    {
                        effectiveRanking = highestOther;
                        result = otherFormatted;
                    }
                    else
                    {
                        if ((IsLocalPlayer && highestOther.IsLocalRanking) ||
                            (!IsLocalPlayer && highestOther.IsTrustedOrganization))
                            effectiveRanking = highestOther;

                        result += " " + otherFormatted;
                    }
                }
                else
                {
                    effectiveRanking = highestOther;
                    result = otherFormatted;
                }
            }

            return result;
        }

        public string Promotion
        {
            get
            {
                if (Rankings == null || !Rankings.Any())
                    return string.Empty;

                // Determine the promotion window based on cutoff date
                // Current month of cutoff, or last month if cutoff is the 1st
                var cutoff = League.CutoffDate;
                DateTime windowStart;
                if (cutoff.Day == 1)
                {
                    // First day of month - include last month
                    windowStart = new DateTime(cutoff.Year, cutoff.Month, 1).AddMonths(-1);
                }
                else
                {
                    // Current month only
                    windowStart = new DateTime(cutoff.Year, cutoff.Month, 1);
                }

                // Find the most recent promotion within the window (before cutoff)
                var recentPromotion = Rankings
                    .Where(r => r.RankingDate.HasValue && 
                           r.RankingDate.Value >= windowStart && 
                           r.RankingDate.Value <= cutoff)
                    .OrderByDescending(r => r.RankingDate)
                    .FirstOrDefault();

                if (recentPromotion == null)
                    return string.Empty;

                // Get combined rankings before and after the promotion
                var promotionDate = recentPromotion.RankingDate.Value;
                string prev = GetCombinedRankingBeforeDate(promotionDate);
                string curr = GetCombinedRankingBeforeDate(promotionDate.AddDays(1));

                if (!string.IsNullOrEmpty(prev) && prev != curr && !prev.Contains('?'))
                    return (prev + "→" + curr).ToUpper();

                return string.Empty;
            }
        }

        public string ShorterName
        {
            get
            {
                var chnName = new string(DisplayName.Where(c => c >= 0x4E00 && c <= 0x9FA5)?.ToArray());
                return string.IsNullOrEmpty(chnName) ? DisplayName : chnName;
            }
        }

        /// <summary>
        /// Ranking History - maps ranking string (in legacy format) to date.
        /// Now loads from PlayerRanking table if available, falling back to parsing Ranking string.
        /// 
        /// Legacy Ranking string format:
        /// 1D:01/06/2024;1K:01/03/2024;;BY:2014 - this is a full record with promotion dates, no ranking stage, and birth year
        /// 2K - only knows this player's current ranking
        /// ;BY:2014 - no record of this player's ranking but only knows the birth year
        /// 
        /// Ranking history is returned in datetime order (latest ranking first)
        /// </summary>
        public Dictionary<string, DateTimeOffset> RankingHistory
        {
            get
            {
                if (_rankingHistory == null)
                {
                    _rankingHistory = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
                    
                    // Load from PlayerRanking table
                    if (Rankings != null && Rankings.Any())
                    {
                        BuildRankingHistoryFromTable();
                    }
                }

                return _rankingHistory;
            }
        }

        /// <summary>
        /// Builds ranking history from PlayerRanking table entries.
        /// </summary>
        private void BuildRankingHistoryFromTable()
        {
            // Order by date descending (latest first)
            var orderedRankings = Rankings
                .OrderByDescending(r => r.RankingDate ?? DateTimeOffset.MinValue)
                .ToList();

            foreach (var ranking in orderedRankings)
            {
                // Use display format as key for backward compatibility with existing code
                string displayKey = FormatRankingForDisplay(ranking);
                DateTimeOffset date = ranking.RankingDate ?? DateTimeOffset.MinValue;
                
                if (!string.IsNullOrEmpty(displayKey) && !_rankingHistory.ContainsKey(displayKey))
                {
                    _rankingHistory.Add(displayKey, date);
                }
            }
        }

        public Dictionary<string, DateTimeOffset> SwaRankingHistory
        {
            get
            {
                if (_swaRankingHistory == null)
                {
                    _swaRankingHistory = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
                    
                    // Directly query SWA rankings from the Rankings table
                    if (Rankings != null && Rankings.Any())
                    {
                        var swaRankings = Rankings
                            .Where(r => r.Organization == "SWA" && !string.IsNullOrEmpty(r.Ranking) && !r.Ranking.Contains("K?"))
                            .OrderByDescending(r => r.RankingDate ?? DateTimeOffset.MinValue)
                            .ToList();

                        foreach (var ranking in swaRankings)
                        {
                            string key = ranking.Ranking.ToUpper();
                            DateTimeOffset date = ranking.RankingDate ?? DateTimeOffset.MinValue;
                            
                            if (!_swaRankingHistory.ContainsKey(key) || _swaRankingHistory[key] > date)
                                _swaRankingHistory[key] = date;
                        }
                    }
                }

                return _swaRankingHistory;
            }
        }

        public string FormatedRankingHistory
        {
            get
            {
                var rankingHistory = RankingHistory.Where(x => x.Key != LatestRanking && !string.IsNullOrEmpty(x.Key) && !x.Key.Contains("K?"));
                if (rankingHistory.Count() > 2)
                    return string.Join(Environment.NewLine, rankingHistory.Select(x => string.Join(":", x.Key, x.Value == DateTimeOffset.MinValue ? "?" : x.Value.ToString(DATE_FORMAT)).Replace(" ", string.Empty)));
                else
                    return string.Empty;
            }
        }

        public string LatestRankingHistory(int noOfRecords, bool swaOnly)
        {
            var source = swaOnly ? SwaRankingHistory : RankingHistory;
            if (!source.Any())
                return string.Empty;
            var latestRanking = source.First().Key;
            var rankingHistory = source.Where(x => x.Key != latestRanking && !string.IsNullOrEmpty(x.Key) && !x.Key.Contains("K?"));
            if (swaOnly)
                rankingHistory = rankingHistory.Where(x => !x.Key.Contains("K"));
            else
                rankingHistory = rankingHistory.Take(noOfRecords);
            return string.Join(". ", rankingHistory.Select(x => x.Value == DateTimeOffset.MinValue ? x.Key : string.Join(":", x.Key, x.Value.ToString(DATE_FORMAT))));
        }

        /// <summary>
        /// Formats a PlayerRanking for display (e.g., "1D" for SWA, "(1D)" for TGA, "[1D]" for foreign)
        /// When SwaOnly mode is enabled, TGA rankings are shown as "[1D]" like foreign rankings.
        /// </summary>
        public static string FormatRankingForDisplay(PlayerRanking playerRanking)
        {
            if (playerRanking == null || string.IsNullOrEmpty(playerRanking.Ranking))
                return string.Empty;

            return FormatRankingForDisplay(playerRanking.Ranking, playerRanking.Organization);
        }

        /// <summary>
        /// Formats a ranking string with organization indicator for display.
        /// </summary>
        /// <param name="ranking">The ranking grade (e.g., "1D", "2K")</param>
        /// <param name="organization">The organization (e.g., "SWA", "TGA")</param>
        /// <returns>Formatted ranking: "1D" for SWA, "(1D)" for TGA, "[1D]" for others</returns>
        public static string FormatRankingForDisplay(string ranking, string organization)
        {
            if (string.IsNullOrEmpty(ranking))
                return string.Empty;

            if (organization == "SWA" || ranking.Contains("P"))
                return ranking;
            // When SwaOnly is enabled, show TGA as [] like foreign rankings
            if (organization == "TGA" && !Engine.Stats.EloStat.SwaOnly)
                return $"({ranking})";
            return $"[{ranking}]";
        }

        public void GetSwaRanking(out string ranking, out string rankedDate)
        {
            ranking = rankedDate = string.Empty;
            if (SwaRankingHistory.Any())
            {
                ranking = SwaRankingHistory.First().Key;
                var dateVal = SwaRankingHistory.First().Value;
                rankedDate = dateVal == DateTimeOffset.MinValue ? string.Empty : dateVal.ToString(DATE_FORMAT);
            }
        }

        public int GetRatingBeforeDate(DateTimeOffset date, bool intl = false)
        {
            // Use combined ranking to get the effective ranking (trusted kyu can override SWA kyu)
            GetCombinedRankingBeforeDate(date, out var effectiveRanking);
            return GetRatingByRanking(effectiveRanking, intl);
        }

        /// <summary>
        /// Gets rating for a ranking using PlayerRanking object.
        /// </summary>
        public int GetRatingByRanking(PlayerRanking playerRanking, bool intl = false)
        {
            if (playerRanking == null || string.IsNullOrEmpty(playerRanking.Ranking))
                return RatingCalculator.DEFAULT_RATING;

            return RatingCalculator.CalculateRating(playerRanking.Ranking, playerRanking.Organization, intl);
        }

        /// <summary>
        /// Gets rating for a ranking string (legacy format support).
        /// </summary>
        public int GetRatingByRanking(string ranking, bool intl = false)
        {
            if (string.IsNullOrEmpty(ranking))
                return RatingCalculator.DEFAULT_RATING;

            var (grade, organization) = RatingCalculator.ParseRankingString(ranking);
            return RatingCalculator.CalculateRating(grade, organization, intl);
        }

        public static string GetEffectiveRanking(string ranking)
        {
            return RatingCalculator.GetEffectiveRanking(ranking);
        }
    }
}
