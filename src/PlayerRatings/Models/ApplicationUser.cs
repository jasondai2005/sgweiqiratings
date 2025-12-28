using Microsoft.AspNetCore.Identity;
using System.Text.RegularExpressions;

namespace PlayerRatings.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        private const string BIRTH_YEAR = "BY";
        internal const string DATE_FORMAT = "dd/MM/yyyy";
        // Rating Scale: 1 dan = 2100, difference between grades = 100
        // Professional: 1p = 7d = 2700, difference between pro grades = 30
        // Minimum rating = -900
        private const int ONE_D_RATING = 2100;
        private const int ONE_P_RATING = 2700; // 1p = 7d
        private const int GRADE_DIFF = 100;
        private const int PRO_GRADE_DIFF = 30;
        private const int MIN_RATING = -900;
        private readonly Dictionary<string, DateTimeOffset> _rankingHistory = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTimeOffset> _swaRankingHistory = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);

        public static List<string> InvisiblePlayers = new List<string>
        {
            "mok.jj@sw.org"
        };

        public string DisplayName { get; set; }

        public string BirthYear
        {
            get
            {
                RankingHistory.TryGetValue(BIRTH_YEAR, out var birthYear);

                return birthYear == DateTimeOffset.MinValue ? string.Empty : birthYear.Year.ToString();
            }
        }

        public string BirthYearU18
        {
            get
            {
                RankingHistory.TryGetValue(BIRTH_YEAR, out var birthYear);

                return birthYear.Year + 18 < DateTime.Now.Year ? string.Empty : birthYear.Year.ToString();
            }
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
                var rankingChangeDeadline = League.CutoffDate.AddMonths(-6);
                return IsVirtualPlayer || League.CutoffDate.AddYears(-2) < LastMatch ||
                    (RankingBeforeCutoffDate?.Contains('K', StringComparison.InvariantCultureIgnoreCase) == true &&
                    RankingBeforeCutoffDate != GetRankingBeforeDate(rankingChangeDeadline));
            }
        }

        public string Ranking { get; set; }

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
                    m_initRanking = GetRankingBeforeDate(FirstMatch.Date);

                return m_initRanking;
            }
        }

        public bool IsVirtualPlayer => DisplayName.Contains('[');
        public bool IsUnknownPlayer => string.IsNullOrEmpty(InternalInitRanking) || InternalInitRanking.Contains("K?");
        public bool IsUnknownRankedPlayer => IsUnknownPlayer || (InternalInitRanking.Contains('[') && !InternalInitRanking.Contains(' '));
        public bool IsNewUnknownRankdedPlayer => MatchCount <= 12 && IsUnknownRankedPlayer;
        public bool IsProPlayer => RankingBeforeCutoffDate.Contains('P');
        public bool IsNewKyuPlayer => MatchCount <= 12 && !InternalInitRanking.Contains('D') && !IsProPlayer && !IsVirtualPlayer;
        public bool IsHiddenPlayer => (InvisiblePlayers.Contains(Email, StringComparer.OrdinalIgnoreCase) || IsNewKyuPlayer || IsNewUnknownRankdedPlayer);

        // Number of games to monitor for local dan players and returning players
        private const int LOCAL_PLAYER_GAMES_THRESHOLD = 6;

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
                
                // Must be a local dan player (has 'D', no square brackets which indicate foreign ranking)
                // SWA: "2D" (no brackets)
                // TGA: "(2D)" (parentheses) 
                // Foreign: "[2D]" (square brackets) - excluded
                var ranking = InternalInitRanking ?? string.Empty;
                bool isDan = ranking.Contains('D');
                bool isLocalRanking = !ranking.Contains('[');
                
                return isDan && isLocalRanking;
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

        public string LatestRanking => GetRankingBeforeDate(DateTimeOffset.Now.AddDays(1));

        public string LatestRankedDate
        {
            get
            {
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
                return GetRankingBeforeDate(League.CutoffDate);
            }
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
                    var firstPromotionDate = RankingHistory.Where(x => x.Key != BIRTH_YEAR).Last().Value;
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
        /// Ranking History raw data samples:
        /// 1D:01/06/2024;1K:01/03/2024;;BY:2014 - this is a full record with promotion dates, no ranking stage, and birth year
        /// or
        /// 2K - only knows this player's current ranking
        /// or
        /// ;BY:2014 - no record of this player's ranking but only knows the birth year
        /// 
        /// Ranking history has to be recorded in datetime order (latest ranking first)
        /// </summary>
        public Dictionary<string, DateTimeOffset> RankingHistory
        {
            get
            {
                if (!_rankingHistory.Any() || Ranking?.StartsWith(_rankingHistory.First().Key) == false)
                {
                    var ranking = Ranking ?? string.Empty;
                    var rankingHistory = ranking.Split(";");
                    foreach (var entry in rankingHistory)
                    {
                        var kvPair = entry.Split(':');
                        if (kvPair.Length > 1)
                        {
                            var rankingDate = DateTimeOffset.ParseExact(kvPair[1], kvPair[0] == BIRTH_YEAR ? "yyyy" : DATE_FORMAT, null);
                            _rankingHistory.Add(kvPair[0], rankingDate);
                        }
                        else
                        {
                            _rankingHistory.Add(kvPair[0], DateTimeOffset.MinValue);
                        }
                    }
                }

                return _rankingHistory;
            }
        }

        public Dictionary<string, DateTimeOffset> SwaRankingHistory
        {
            get
            {
                if (!_swaRankingHistory.Any())
                {
                    foreach (var ranking in RankingHistory.Keys)
                    {
                        if (ranking == BIRTH_YEAR)
                            continue;
                        var swaRanking = GetSwaRanking(ranking);
                        if (!_swaRankingHistory.ContainsKey(swaRanking) || _swaRankingHistory[swaRanking] > _rankingHistory[ranking])
                            _swaRankingHistory[swaRanking] = _rankingHistory[ranking];
                    }
                }

                return _swaRankingHistory;
            }
        }

        public string FormatedRankingHistory
        {
            get
            {
                var rankingHistory = RankingHistory.Where(x => x.Key != BIRTH_YEAR && x.Key != LatestRanking && !string.IsNullOrEmpty(x.Key) && !x.Key.Contains("K?"));
                if (rankingHistory.Count() > 2)
                    return string.Join(Environment.NewLine, rankingHistory.Select(x => string.Join(":", x.Key, x.Value == DateTimeOffset.MinValue ? "?" : x.Value.ToString(DATE_FORMAT)).Replace(" ", string.Empty)));
                else
                    return string.Empty;
            }
        }

        public string LatestRankingHistory(int noOfRecords, bool swaOnly)
        {
            var source = swaOnly ? SwaRankingHistory : RankingHistory;
            var latestRanking = source.First().Key;
            var rankingHistory = source.Where(x => x.Key != BIRTH_YEAR && x.Key != latestRanking && !string.IsNullOrEmpty(x.Key) && !x.Key.Contains("K?"));
            if (swaOnly)
                rankingHistory = rankingHistory.Where(x => !x.Key.Contains("K"));
            else
                rankingHistory = rankingHistory.Take(noOfRecords);
            return string.Join(". ", rankingHistory.Select(x => x.Value == DateTimeOffset.MinValue ? x.Key : string.Join(":", x.Key, x.Value.ToString(DATE_FORMAT))));
        }

        public string GetRankingBeforeDate(DateTimeOffset date)
        {
            var rankingHistory = RankingHistory.Where(x => x.Key != BIRTH_YEAR);
            var ranking = rankingHistory.FirstOrDefault(x => x.Value < date);
            if (ranking.Key == null)
            {
                // first recorded ranking could be used here
                // - if this player's no-ranking stage should be emphasized,
                //   record it in the history
                ranking = rankingHistory.Last();
            }
            return ranking.Key;
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

        public int GetRatingByRanking(string ranking, bool intl = false)
        {
            if (string.IsNullOrEmpty(ranking))
                return GetKyuRating(11); // Default to 11 kyu = 1000

            ranking = GetEffectiveRanking(ranking);

            bool isPro = ranking.Contains('P');
            bool isDan = ranking.Contains('D');
            bool isKyu = ranking.Contains('K');
            bool isForeign = ranking.Contains('['); 
            bool isSWA = !isForeign && !ranking.Contains('(');
            int.TryParse(Regex.Match(ranking, @"\d+").Value, out int rankingNum);

            int delta = 0;
            if (!isSWA && !intl)
            {
                if (isDan)
                {
                    // TGA dan ratings are 100 points lower than SWA (one level down)
                    // except (1D) which is 2050 (to be higher than 1K=1900)
                    if (rankingNum == 1)
                        delta = -50;  // (1D) = 2050
                    else
                        rankingNum -= 1;  // (2D) = 2100, (3D) = 2200, etc.
                }
                else if (isKyu && rankingNum <= 5)
                {
                    // TGA kyu 1K-5K are adjusted down one level
                    rankingNum += 1;
                    // TGA (5K) -> 1550 (between SWA 5K=1600 and 6K=1500)
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
