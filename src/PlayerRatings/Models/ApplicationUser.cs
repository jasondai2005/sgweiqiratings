using Microsoft.AspNetCore.Identity;
using System.Text.RegularExpressions;

namespace PlayerRatings.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        private const string BIRTH_YEAR = "BY";
        internal const string DATE_FORMAT = "dd/MM/yyyy";
        private const int ONE_P_RATING = 2240;
        private const int ONE_D_RATING = 1800; // 2000;
        private const int DAN_RANKING_DIFF = 40;
        private const int KYU_RANKING_DIFF_HIGH = 10;
        private const int KYU_RANKING_DIFF_LOW = 2;
        private Dictionary<string, DateTimeOffset> _rankingHistory = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);

        public static List<string> InvisiblePlayers = new List<string>()
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

        public bool Active
        {
            get
            {
                var rankingChangeDeadline = League.CutoffDate.AddMonths(-6);
                return IsVirtualPlayer || League.CutoffDate.AddYears(-1) < LastMatch ||
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
        public bool IsForeignRankedPlayer => InternalInitRanking.Contains('[') && !InternalInitRanking.Contains(' ');
        public bool IsNewForeignPlayer => MatchCount <= 12 && IsForeignRankedPlayer;
        public bool IsProPlayer => RankingBeforeCutoffDate.Contains('P');
        public bool IsNewKyuPlayer => MatchCount <= 12 && !InternalInitRanking.Contains('D') && !IsProPlayer && !IsVirtualPlayer;
        public bool IsHiddenPlayer => (InvisiblePlayers.Contains(Email, StringComparer.OrdinalIgnoreCase) || IsNewKyuPlayer || IsNewForeignPlayer);

        public bool NeedDynamicFactor(bool intl)
        {
            return intl ? MatchCount <= 12 : IsNewForeignPlayer;
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
                    for (int i = 0; i < rankingHistory.Length; i++)
                    {
                        try
                        {
                            var kvPair = rankingHistory[i].Split(':');
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
                        catch
                        {
                            throw;
                        }
                    }
                }

                return _rankingHistory;
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

        public string LatestRankingHistory(int noOfRecords)
        {
            var rankingHistory = RankingHistory.Where(x => x.Key != BIRTH_YEAR && x.Key != LatestRanking && !string.IsNullOrEmpty(x.Key) && !x.Key.Contains("K?")).Take(noOfRecords);
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
            foreach (var pair in RankingHistory.Where(x => x.Key != BIRTH_YEAR))
            {
                ranking = GetSwaRanking(pair.Key);
                if (!string.IsNullOrEmpty(ranking))
                {
                    rankedDate = pair.Value == DateTimeOffset.MinValue ? string.Empty : pair.Value.ToString(DATE_FORMAT);
                    return;
                }
            }
        }

        private string GetSwaRanking(string ranking)
        {
            if (string.IsNullOrEmpty(ranking) || ranking.Contains("K?") || ranking.Contains('P'))
                return string.Empty;

            return ranking.Contains('[') || ranking.Contains('(') ? ranking.Remove(Math.Max(0, ranking.IndexOfAny(new char[] { '[', '(' }) - 1)) : ranking;
        }

        public int GetRatingBeforeDate(DateTimeOffset date, bool intl = false, bool protectd = false)
        {
            var ranking = GetRankingBeforeDate(date);
            return GetRatingByRanking(ranking, intl, protectd);
        }

        public int GetRatingByRanking(string ranking, bool intl = false, bool protectd = false)
        {
            if (string.IsNullOrEmpty(ranking))
                return protectd ? GetKyuRating(11, true) : GetKyuRating(5, false);

            ranking = GetEffectiveRanking(ranking);

            bool isForeign = ranking.Contains('[');
            if (protectd && (isForeign || ranking.Contains('?')))
                return GetKyuRating(11, true);

            bool isPro = ranking.Contains('P');
            bool isDan = ranking.Contains('D');
            bool isKyu = ranking.Contains('K');
            bool isSWA = !isForeign && !ranking.Contains('(');
            int.TryParse(Regex.Match(ranking, @"\d+").Value, out int rankingNum);

            if (protectd && (isPro || (isDan && rankingNum >= 5)))
                return GetDanRating(5);

            if (isPro)
            {
                return GetProRating(rankingNum);
            }
            else if (isDan)
            {
                switch (rankingNum)
                {
                    case 8:
                        return ONE_D_RATING == 2000 || intl ? GetDanRating(rankingNum) : ONE_P_RATING;
                    case 7:
                        return ONE_D_RATING == 2000 || intl ? GetDanRating(rankingNum) : ONE_P_RATING - 120;
                    case 6:
                    case 5:
                        return GetDanRating(rankingNum);
                    case 4:
                    case 3:
                    case 2:
                        return isSWA || intl ? GetDanRating(rankingNum) : GetDanRating(rankingNum - 1);
                    case 1:
                        return isSWA || intl ? ONE_D_RATING : ONE_D_RATING - 30;
                    default:
                        throw new Exception("Unknown ranking.");
                }
            }
            else if (isKyu)
            {
                switch (rankingNum)
                {
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                        return isSWA || intl ? GetKyuRating(rankingNum, protectd) : GetKyuRating(rankingNum + 1, protectd);
                    case 5:
                        return isSWA || intl ? GetKyuRating(5, protectd) : GetKyuRating(5, protectd) - 5;
                    case 6:
                    case 7:
                    case 8:
                    case 9:
                    case 10:
                        return GetKyuRating(rankingNum, protectd || intl);
                    default:
                        return GetKyuRating(11, protectd || intl);
                }
            }
            else
            {
                switch (ranking)
                {
                    case "♔": // national champion
                        return 2600;
                    case "♕": // international champion
                        return 2640;
                    default:
                        return GetKyuRating(11, protectd);
                }
            }
        }

        public static string GetEffectiveRanking(string ranking)
        {
            ranking = ranking.ToUpper();
            if (ranking.Contains(' '))
            {
                bool useSwaRanking = ranking.Contains('[') ||
                    (ranking.Contains('(') && !ranking.Contains("5D"));
                if (useSwaRanking)
                    ranking = ranking.Substring(0, ranking.IndexOf(' '));
                else
                    ranking = ranking.Substring(ranking.IndexOf('('));
            }

            return ranking;
        }

        private int GetProRating(int pro)
        {
            return ONE_P_RATING + (pro - 1) * DAN_RANKING_DIFF;
        }

        private int GetDanRating(int dan)
        {
            return ONE_D_RATING + (dan - 1) * DAN_RANKING_DIFF;
        }

        private int GetKyuRating(int kyu, bool useDefaultRankingDiff)
        {
            if (useDefaultRankingDiff)
                return ONE_D_RATING - kyu * DAN_RANKING_DIFF;

            return kyu <= 6 ?
                (ONE_D_RATING - DAN_RANKING_DIFF) - (kyu - 1) * KYU_RANKING_DIFF_HIGH :
                (GetKyuRating(6, false) - (kyu - 6) * KYU_RANKING_DIFF_LOW);
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
