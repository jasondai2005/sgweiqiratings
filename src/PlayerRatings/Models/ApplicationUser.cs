using Microsoft.AspNetCore.Identity;
using System.Text.RegularExpressions;

namespace PlayerRatings.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        internal const string DATE_FORMAT = "dd/MM/yyyy";
        // Rating Scale: 1 dan = 2100, difference between grades = 100
        // Professional: 1p = 7d = 2700, difference between pro grades = 30
        // Minimum rating = -900
        private const int ONE_D_RATING = 2100;
        private const int ONE_P_RATING = 2700; // 1p = 7d
        private const int GRADE_DIFF = 100;
        private const int PRO_GRADE_DIFF = 30;
        private const int MIN_RATING = -900;
        private Dictionary<string, DateTimeOffset> _rankingHistory = null;
        private Dictionary<string, DateTimeOffset> _swaRankingHistory = null;

        public static List<string> InvisiblePlayers = new List<string>
        {
            "mok.jj@sw.org"
        };

        public string DisplayName { get; set; }

        /// <summary>
        /// Player's birth year (new column, replaces BY: in Ranking string)
        /// </summary>
        public int? BirthYearValue { get; set; }

        /// <summary>
        /// Player's residence/location. Determines if player is local.
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
        /// Determines if this is a local player based on Residence
        /// </summary>
        public bool IsLocalPlayer => string.IsNullOrEmpty(Residence) || Residence == "Singapore";

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
                var rankingChangeDeadline = League.CutoffDate.AddMonths(-6);
                return IsVirtualPlayer || League.CutoffDate.AddYears(-2) < LastMatch ||
                    (RankingBeforeCutoffDate?.Contains('K', StringComparison.InvariantCultureIgnoreCase) == true &&
                    RankingBeforeCutoffDate != GetRankingBeforeDate(rankingChangeDeadline));
            }
        }

        public string InitRanking
        {
            get
            {
                return InternalInitRanking == RankingBeforeCutoffDate?.ToUpper() ?
                    string.Empty :
                    (string.IsNullOrEmpty(InternalInitRanking) ? "unkwn" : InternalInitRanking);
            }
        }

        private string m_initRanking = null;
        internal string InternalInitRanking
        {
            get
            {
                if (m_initRanking == null)
                {
                    m_initRanking = GetRankingBeforeDate(FirstMatch.Date);
                }

                return m_initRanking;
            }
        }

        public bool IsVirtualPlayer => DisplayName?.Contains('[') ?? false;
        public bool IsUnknownPlayer => string.IsNullOrEmpty(InternalInitRanking) || InternalInitRanking.Contains("K?");
        
        /// <summary>
        /// Determines if this is a player with an unknown/foreign ranking at the time of their first match.
        /// Foreign rankings are indicated by IsForeignRanking in the PlayerRanking structure.
        /// </summary>
        public bool IsUnknownRankedPlayer
        {
            get
            {
                if (IsUnknownPlayer)
                    return true;
                
                // Get the ranking at the time of first match (or earliest if no match yet)
                var initialRanking = GetPlayerRankingBeforeDate(FirstMatch);
                
                if (initialRanking != null && initialRanking.IsForeignRanking && !initialRanking.Ranking.Contains("P"))
                {
                    return true;
                }
                
                return false;
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
                if (MatchCount > 12 || IsProPlayer || IsVirtualPlayer)
                    return false;
                    
                var ranking = InternalInitRanking ?? string.Empty;
                return !ranking.Contains('D', StringComparison.OrdinalIgnoreCase);
            }
        }
        
        public bool IsHiddenPlayer => (InvisiblePlayers.Contains(Email, StringComparer.OrdinalIgnoreCase) || IsNewKyuPlayer || IsNewUnknownRankdedPlayer);

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
                if (MatchCount > LOCAL_PLAYER_GAMES_THRESHOLD || IsVirtualPlayer || IsProPlayer)
                    return false;
                
                // Must have first match in 2025 or later
                if (FirstMatch == DateTimeOffset.MinValue || FirstMatch.Year < 2025)
                    return false;
                
                var initialRanking = GetPlayerRankingBeforeDate(FirstMatch);
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
                if (IsVirtualPlayer)
                    return false;
                
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
        /// Shows SWA ranking plus highest other ranking (TGA or foreign) only if it's higher than SWA.
        /// Examples: "1D (2D)" for SWA 1D + TGA 2D (2D > 1D), "3D" for SWA 3D + TGA 1D (1D < 3D)
        /// </summary>
        public string GetCombinedRankingBeforeDate(DateTimeOffset date)
        {
            if (Rankings == null || !Rankings.Any())
                return GetRankingBeforeDate(date);

            // Get latest SWA ranking before date
            var latestSwa = Rankings
                .Where(r => r.Organization == "SWA" && !string.IsNullOrEmpty(r.Ranking))
                .Where(r => r.RankingDate == null || r.RankingDate < date)
                .OrderByDescending(r => r.RankingDate ?? DateTimeOffset.MinValue)
                .ThenByDescending(r => GetRatingByRanking(r))
                .FirstOrDefault();

            // Get highest ranking from any other organization before date
            var highestOther = Rankings
                .Where(r => r.Organization != "SWA" && !string.IsNullOrEmpty(r.Ranking))
                .Where(r => r.RankingDate == null || r.RankingDate < date)
                .OrderByDescending(r => GetRatingByRanking(r))
                .ThenByDescending(r => r.RankingDate ?? DateTimeOffset.MinValue)
                .FirstOrDefault();

            // Build result
            string result = latestSwa?.Ranking ?? string.Empty;
            int swaRating = latestSwa != null ? GetRatingByRanking(latestSwa) : 0;
            int otherRating = highestOther != null ? GetRatingByRanking(highestOther) : 0;

            // Only show other ranking if it's higher than or equal to SWA rating (equal rating means higher rank)
            if (highestOther != null && otherRating >= swaRating)
            {
                string otherFormatted = FormatRankingForDisplay(highestOther);
                if (!string.IsNullOrEmpty(result))
                {
                    if (highestOther.Ranking.Contains("P") || (result.Contains("K") && highestOther.IsLocalRanking))
                        result = otherFormatted;
                    else
                        result += " " + otherFormatted;
                }
                else
                    result = otherFormatted;
            }

            return string.IsNullOrEmpty(result) ? GetRankingBeforeDate(date) : result;
        }

        public string Promotion
        {
            get
            {
                var curRanking = RankingBeforeCutoffDate;
                string prevRanking;
                if (League.CutoffDate.Year == 2023)
                {
                    prevRanking = FirstMatch < League.CutoffDate ? InternalInitRanking : string.Empty;
                }
                else
                {
                    var prevMonth = new DateTimeOffset(League.CutoffDate.Year, League.CutoffDate.AddDays(-1).Month, 1, 0, 0, 0, TimeSpan.Zero).AddDays(-1);
                    if (!RankingHistory.Any())
                    {
                        // No ranking history - no promotion to show
                        return string.Empty;
                    }
                    var firstPromotionDate = RankingHistory.Last().Value;
                    prevRanking = (firstPromotionDate > prevMonth && firstPromotionDate < League.CutoffDate) ? string.Empty : GetRankingBeforeDate(prevMonth);
                }

                if (prevRanking != curRanking)
                    return ((prevRanking.Contains('?') ? string.Empty : prevRanking) + "→" + curRanking).ToUpper();

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
                    
                    foreach (var ranking in RankingHistory.Keys)
                    {
                        var swaRanking = GetSwaRanking(ranking);
                        if (!string.IsNullOrEmpty(swaRanking))
                        {
                            if (!_swaRankingHistory.ContainsKey(swaRanking) || _swaRankingHistory[swaRanking] > _rankingHistory[ranking])
                                _swaRankingHistory[swaRanking] = _rankingHistory[ranking];
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
        /// Gets the player's PlayerRanking object before a specific date.
        /// Returns null if no ranking is found.
        /// Rankings without dates are treated as the earliest (assumed to be initial rankings).
        /// </summary>
        public PlayerRanking GetPlayerRankingBeforeDate(DateTimeOffset date)
        {
            if (Rankings == null || !Rankings.Any())
                return null;

            // Get rankings ordered by date descending (latest first)
            // Rankings without dates are treated as very early (MinValue)
            var orderedRankings = Rankings
                .OrderByDescending(r => r.RankingDate ?? DateTimeOffset.MinValue)
                .ToList();
            
            // Find the first ranking that was achieved before the date
            // Rankings without dates (MinValue) are always considered "before" any real date
            var ranking = orderedRankings.FirstOrDefault(r => (r.RankingDate ?? DateTimeOffset.MinValue) < date);
            
            if (ranking != null)
                return ranking;
            
            // If no ranking before the date, return the earliest known ranking
            // Rankings without dates are considered the earliest
            return Rankings
                .OrderBy(r => r.RankingDate ?? DateTimeOffset.MinValue)
                .FirstOrDefault();
        }

        /// <summary>
        /// Gets the player's ranking string before a specific date (for display purposes).
        /// Returns the ranking grade with organization indicator.
        /// </summary>
        public string GetRankingBeforeDate(DateTimeOffset date)
        {
            var playerRanking = GetPlayerRankingBeforeDate(date);
            return FormatRankingForDisplay(playerRanking);
        }

        /// <summary>
        /// Formats a PlayerRanking for display (e.g., "1D" for SWA, "(1D)" for TGA, "[1D]" for foreign)
        /// </summary>
        public static string FormatRankingForDisplay(PlayerRanking playerRanking)
        {
            if (playerRanking == null || string.IsNullOrEmpty(playerRanking.Ranking))
                return string.Empty;

            string org = playerRanking.Organization;
            string ranking = playerRanking.Ranking;

            if (org == "SWA" || ranking.Contains("P"))
                return ranking;
            if (org == "TGA")
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

        private string GetSwaRanking(string ranking)
        {
            if (string.IsNullOrEmpty(ranking) || ranking.Contains("K?") || ranking.Contains('P'))
                return string.Empty;

            return ranking.Contains('[') || ranking.Contains('(') ? ranking.Remove(Math.Max(0, ranking.IndexOfAny(new char[] { '[', '(' }) - 1)) : ranking;
        }

        public int GetRatingBeforeDate(DateTimeOffset date, bool intl = false)
        {
            var ranking = GetRankingBeforeDate(date);
            return GetRatingByRanking(ranking, intl);
        }

        /// <summary>
        /// Gets rating for a ranking using PlayerRanking object.
        /// </summary>
        public int GetRatingByRanking(PlayerRanking playerRanking, bool intl = false)
        {
            if (playerRanking == null || string.IsNullOrEmpty(playerRanking.Ranking))
                return GetKyuRating(11); // Default to 11 kyu = 1000

            string rankingGrade = playerRanking.Ranking.ToUpper();
            string organization = playerRanking.Organization;

            return CalculateRating(rankingGrade, organization, intl);
        }

        /// <summary>
        /// Gets rating for a ranking string (legacy format support).
        /// </summary>
        public int GetRatingByRanking(string ranking, bool intl = false)
        {
            if (string.IsNullOrEmpty(ranking))
                return GetKyuRating(11); // Default to 11 kyu = 1000

            ranking = GetEffectiveRanking(ranking);

            // Determine organization from legacy format
            string organization;
            string rankingGrade;
            
            if (ranking.Contains('['))
            {
                // Foreign: [1D CWA] -> extract organization
                organization = "Foreign"; // Treated as foreign for rating purposes
                var match = Regex.Match(ranking, @"\[([^\s]+)\s*([^\]]*)\]");
                rankingGrade = match.Success ? match.Groups[1].Value : ranking.Replace("[", "").Replace("]", "");
            }
            else if (ranking.Contains('('))
            {
                // TGA: (1D)
                organization = "TGA";
                rankingGrade = ranking.Replace("(", "").Replace(")", "");
            }
            else
            {
                // SWA: 1D
                organization = "SWA";
                rankingGrade = ranking;
            }

            return CalculateRating(rankingGrade, organization, intl);
        }

        /// <summary>
        /// Core rating calculation based on ranking grade and organization.
        /// </summary>
        private int CalculateRating(string rankingGrade, string organization, bool intl)
        {
            rankingGrade = rankingGrade.ToUpper();
            
            bool isPro = rankingGrade.Contains('P');
            bool isDan = rankingGrade.Contains('D');
            bool isKyu = rankingGrade.Contains('K');
            bool isSWA = organization == "SWA";
            int.TryParse(Regex.Match(rankingGrade, @"\d+").Value, out int rankingNum);

            int delta = 0;
            if (!isSWA && !intl)
            {
                if (isDan)
                {
                    // Foreign dan ratings are 100 points lower than SWA (one level down)
                    // except (1D) which is 2050 (to be higher than 1K=1900)
                    if (rankingNum == 1)
                        delta = -50;  // (1D) = 2050
                    else
                        rankingNum -= 1;  // (2D) = 2100, (3D) = 2200, etc.
                }
                else if (isKyu && rankingNum <= 5)
                {
                    // Foreign kyu 1K-5K are adjusted down one level
                    rankingNum += 1;
                    // Foreign (5K) -> 1550 (between SWA 5K=1600 and 6K=1500)
                    if (rankingNum == 6)
                        delta = 50;
                }
            }

            if (isPro)
            {
                // 1p = 2700, 2p = 2730, ..., 9p = 2940
                return GetProRating(Math.Min(rankingNum, 9));
            }
            else if (isDan)
            {
                // 1d = 2100, 2d = 2200, ..., 6d = 2600, 7d+ = use dan rating
                return GetDanRating(rankingNum) + delta;
            }
            else if (isKyu)
            {
                // 1k = 2000, 2k = 1900, ..., 20k = 100
                return GetKyuRating(Math.Min(rankingNum, 30)) + delta;
            }
            else
            {
                return GetKyuRating(11); // Default to 11 kyu = 1000
            }
        }

        public static string GetEffectiveRanking(string ranking)
        {
            ranking = ranking.ToUpper();
            if (ranking.Contains(' '))
            {
                bool useSwaRanking = ranking.Contains('[') || ranking.Contains('(');
                if (useSwaRanking)
                    ranking = ranking.Substring(0, ranking.IndexOf(' '));
                else
                    ranking = ranking.Substring(ranking.IndexOf('('));
            }

            return ranking;
        }

        /// <summary>
        /// Gets rating for professional grade.
        /// 1p = 2700, 2p = 2730, ..., 9p = 2940
        /// </summary>
        private int GetProRating(int pro)
        {
            return ONE_P_RATING + (pro - 1) * PRO_GRADE_DIFF;
        }

        /// <summary>
        /// Gets rating for dan grade.
        /// 1d = 2100, 2d = 2200, ..., 6d = 2600, 7d = 2700 (same as 1p)
        /// </summary>
        private int GetDanRating(int dan)
        {
            return ONE_D_RATING + (dan - 1) * GRADE_DIFF;
        }

        /// <summary>
        /// Gets rating for kyu grade.
        /// 1k = 2000, 2k = 1900, ..., 20k = 100
        /// </summary>
        private int GetKyuRating(int kyu)
        {
            return Math.Max(ONE_D_RATING - kyu * GRADE_DIFF, MIN_RATING);
        }

        public string GetPosition(ref int postion, string leagueName = "")
        {
            if (IsVirtualPlayer)
                return "Intl.";
            if (!Active)
                return "Inact";

            if (IsProPlayer)
                return "Pro";

            if (!leagueName.Contains("Intl.") && IsHiddenPlayer)
                return postion.ToString();

            return postion++.ToString();
        }
    }
}
