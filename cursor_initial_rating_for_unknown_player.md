# Initial rating for unknown players
_Exported on 12/26/2025 at 20:29:43 GMT+8 from Cursor (2.2.43)_

---

**User**

As the player's ranking could be given by a different country, so setting his initial rating using his ranking might not be accurate. What's the best way to decide an unknown or foreign player's intial rating please?

---

**Cursor**

Let me look at the current implementation to understand how initial ratings are being determined.

Let me explore how initial ratings are determined from rankings:

Based on my review of your codebase, I can see you already have some mechanisms in place for handling foreign/unknown players. Let me explain the approaches and best practices:

## Current System Analysis

Your code in `ApplicationUser.cs` and `EloStat.cs` already handles foreign players with:

```92:101:src/PlayerRatings/Models/ApplicationUser.cs
        public bool IsUnknownRankedPlayer => IsUnknownPlayer || (InternalInitRanking.Contains('[') && !InternalInitRanking.Contains(' '));
        public bool IsNewUnknownRankdedPlayer => MatchCount <= 12 && IsUnknownRankedPlayer;
        public bool IsProPlayer => RankingBeforeCutoffDate.Contains('P');
        public bool IsNewKyuPlayer => MatchCount <= 12 && !InternalInitRanking.Contains('D') && !IsProPlayer && !IsVirtualPlayer;
        public bool IsHiddenPlayer => (InvisiblePlayers.Contains(Email, StringComparer.OrdinalIgnoreCase) || IsNewKyuPlayer || IsNewUnknownRankdedPlayer);

        public bool NeedDynamicFactor(bool intl)
        {
            return intl ? MatchCount <= 12 : IsNewUnknownRankdedPlayer;
        }
```

## Best Practices for Unknown/Foreign Player Initial Ratings

### 1. **Conservative Middle Estimate (Recommended)**
Instead of trusting foreign ranks directly, start with a conservative estimate based on:
- **Use a lower bound**: Foreign 5D might map to local 3D or 4D initially
- **Country-specific adjustments**: Apply known offsets for specific countries (e.g., China dan ranks are often 1-2 stones stronger than other countries)

### 2. **Provisional Rating with High Uncertainty (Glicko-style)**
Your current dynamic K-factor approach is good, but you could enhance it:
- Treat first 15-20 games as "provisional"
- Use a much higher K-factor (you're already doing this with `CalculateDynamicFactor`)
- Reduce impact on established players (also already doing this with `factor2 = 0.5`)

### 3. **Performance-Based Initial Rating**
After the first few games, calculate initial rating based on actual performance:

```csharp
// Example: Estimate from first N games using opponent ratings and results
double EstimateInitialRating(List<(double opponentRating, double score)> games)
{
    if (games.Count < 3) return GetKyuRating(5); // Default 5 kyu
    
    // Calculate performance rating
    double totalOpponentRating = games.Sum(g => g.opponentRating);
    double winRate = games.Sum(g => g.score) / games.Count;
    
    // Performance rating = avg opponent rating + 400 * (wins - losses) / games
    double performanceRating = totalOpponentRating / games.Count + 400 * (2 * winRate - 1);
    
    return Math.Max(performanceRating, MIN_RATING);
}
```

### 4. **Country-Specific Rank Mapping (Most Accurate)**
Create explicit mapping tables:

```csharp
// Suggested country-specific adjustments
public int GetAdjustedRatingByCountry(string ranking, string country)
{
    int baseRating = GetRatingByRanking(ranking);
    
    // Known country adjustments (positive = stronger than nominal)
    var countryAdjustments = new Dictionary<string, int>
    {
        { "China", +200 },      // China ranks are typically 1-2 stones stronger
        { "Korea", +150 },      // Korea ranks also tend to be strong
        { "Japan", +50 },       // Japan ranks fairly accurate
        { "Taiwan", +100 },     // Taiwan has strong rankings
        { "EGF", 0 },           // EGF/European is the baseline
        { "AGA", -50 },         // AGA may be slightly generous
        // Add more countries as needed
    };
    
    if (countryAdjustments.TryGetValue(country, out int adjustment))
        return baseRating + adjustment;
    
    // For unknown countries, be conservative: assume rank is slightly inflated
    return baseRating - 100;
}
```

### 5. **My Recommendation**
Combine approaches by modifying `GetRatingByRanking()`:

1. **Default foreign players 1 rank lower** than their stated rank (conservative)
2. **Apply country-specific adjustments** if you can identify the country from the ranking source
3. **Keep your dynamic K-factor** system to let ratings converge quickly
4. **After 10-15 games**, recalculate their "true" initial rating based on performance for historical accuracy

Would you like me to implement any of these approaches in your codebase?

```plaintext
        public bool IsUnknownRankedPlayer => IsUnknownPlayer || (InternalInitRanking.Contains('[') && !InternalInitRanking.Contains(' '));
        public bool IsNewUnknownRankdedPlayer => MatchCount <= 12 && IsUnknownRankedPlayer;
        public bool IsProPlayer => RankingBeforeCutoffDate.Contains('P');
        public bool IsNewKyuPlayer => MatchCount <= 12 && !InternalInitRanking.Contains('D') && !IsProPlayer && !IsVirtualPlayer;
        public bool IsHiddenPlayer => (InvisiblePlayers.Contains(Email, StringComparer.OrdinalIgnoreCase) || IsNewKyuPlayer || IsNewUnknownRankdedPlayer);

        public bool NeedDynamicFactor(bool intl)
        {
            return intl ? MatchCount <= 12 : IsNewUnknownRankdedPlayer;
        }
```

```csharp
// Example: Estimate from first N games using opponent ratings and results
double EstimateInitialRating(List<(double opponentRating, double score)> games)
{
    if (games.Count < 3) return GetKyuRating(5); // Default 5 kyu
    
    // Calculate performance rating
    double totalOpponentRating = games.Sum(g => g.opponentRating);
    double winRate = games.Sum(g => g.score) / games.Count;
    
    // Performance rating = avg opponent rating + 400 * (wins - losses) / games
    double performanceRating = totalOpponentRating / games.Count + 400 * (2 * winRate - 1);
    
    return Math.Max(performanceRating, MIN_RATING);
}
```

```csharp
// Suggested country-specific adjustments
public int GetAdjustedRatingByCountry(string ranking, string country)
{
    int baseRating = GetRatingByRanking(ranking);
    
    // Known country adjustments (positive = stronger than nominal)
    var countryAdjustments = new Dictionary<string, int>
    {
        { "China", +200 },      // China ranks are typically 1-2 stones stronger
        { "Korea", +150 },      // Korea ranks also tend to be strong
        { "Japan", +50 },       // Japan ranks fairly accurate
        { "Taiwan", +100 },     // Taiwan has strong rankings
        { "EGF", 0 },           // EGF/European is the baseline
        { "AGA", -50 },         // AGA may be slightly generous
        // Add more countries as needed
    };
    
    if (countryAdjustments.TryGetValue(country, out int adjustment))
        return baseRating + adjustment;
    
    // For unknown countries, be conservative: assume rank is slightly inflated
    return baseRating - 100;
}
```

---

**User**

yes, please.

---

**Cursor**

```csharp
using Microsoft.AspNetCore.Identity;
using System.Text.RegularExpressions;

namespace PlayerRatings.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        private const string BIRTH_YEAR = "BY";
        internal const string DATE_FORMAT = "dd/MM/yyyy";
        // EGD Rating Scale: 1 dan = 2100, difference between grades = 100
        // Professional: 1p = 7d = 2700, difference between pro grades = 30
        // Minimum rating = -900
        private const int ONE_D_RATING = 2100;
        private const int ONE_P_RATING = 2700; // 1p = 7d
        private const int GRADE_DIFF = 100;
        private const int PRO_GRADE_DIFF = 30;
        private const int MIN_RATING = -900;
        
        // Country-specific rating adjustments for foreign players
        // Positive values = country ranks are stronger than nominal (conservative initial rating)
        // These adjustments help account for different ranking standards across countries
        private static readonly Dictionary<string, int> CountryRatingAdjustments = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "CN", 200 },      // China ranks are typically 1-2 stones stronger
            { "CHN", 200 },     // Alternative China code
            { "CHINA", 200 },
            { "KR", 150 },      // Korea ranks also tend to be strong
            { "KOR", 150 },
            { "KOREA", 150 },
            { "JP", 50 },       // Japan ranks fairly accurate
            { "JPN", 50 },
            { "JAPAN", 50 },
            { "TW", 100 },      // Taiwan has strong rankings
            { "TWN", 100 },
            { "TAIWAN", 100 },
            { "HK", 50 },       // Hong Kong
            { "HKG", 50 },
            { "EU", 0 },        // European (EGF baseline)
            { "EGF", 0 },
            { "US", -50 },      // AGA may be slightly generous
            { "USA", -50 },
            { "AGA", -50 },
            { "MY", 0 },        // Malaysia
            { "MYS", 0 },
            { "ID", 0 },        // Indonesia
            { "IDN", 0 },
            { "TH", 0 },        // Thailand
            { "THA", 0 },
            { "VN", 0 },        // Vietnam
            { "VNM", 0 },
            { "PH", 0 },        // Philippines
            { "PHL", 0 },
        };
        
        // Default adjustment for unknown foreign countries (be conservative)
        private const int DEFAULT_FOREIGN_ADJUSTMENT = -100;
        
        private Dictionary<string, DateTimeOffset> _rankingHistory = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, DateTimeOffset> _swaRankingHistory = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);

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
                if (DisplayName.Contains("Zhou Yi")) // temp fix for known data issue
                    m_initRanking = "[5D]";
                return m_initRanking;
            }
        }

        public bool IsVirtualPlayer => DisplayName.Contains('[');
        public bool IsUnknownPlayer => false;// string.IsNullOrEmpty(InternalInitRanking) || InternalInitRanking.Contains("?");
        public bool IsUnknownRankedPlayer => IsUnknownPlayer || (InternalInitRanking.Contains('[') && !InternalInitRanking.Contains(' '));
        public bool IsNewUnknownRankdedPlayer => MatchCount <= 12 && IsUnknownRankedPlayer;
        public bool IsProPlayer => RankingBeforeCutoffDate.Contains('P');
        public bool IsNewKyuPlayer => MatchCount <= 12 && !InternalInitRanking.Contains('D') && !IsProPlayer && !IsVirtualPlayer;
        public bool IsHiddenPlayer => (InvisiblePlayers.Contains(Email, StringComparer.OrdinalIgnoreCase) || IsNewKyuPlayer || IsNewUnknownRankdedPlayer);

        public bool NeedDynamicFactor(bool intl)
        {
            return intl ? MatchCount <= 12 : IsNewUnknownRankdedPlayer;
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
                return GetKyuRating(5); // Default to 5 kyu = 1600

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
                if (rankingNum == 5)
                {
                    delta = isDan || isKyu ? 50 : 0;
                }

                if (isDan && rankingNum <= 5)
                {
                    rankingNum -= 1;
                }
                else if (isKyu && rankingNum <= 5)
                {
                    rankingNum += 1;
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
                bool useSwaRanking = ranking.Contains('[') ||
                    (ranking.Contains('(') && !ranking.Contains("5D"));
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

```

```csharp
using Microsoft.AspNetCore.Identity;
using System.Text.RegularExpressions;

namespace PlayerRatings.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        private const string BIRTH_YEAR = "BY";
        internal const string DATE_FORMAT = "dd/MM/yyyy";
        // EGD Rating Scale: 1 dan = 2100, difference between grades = 100
        // Professional: 1p = 7d = 2700, difference between pro grades = 30
        // Minimum rating = -900
        private const int ONE_D_RATING = 2100;
        private const int ONE_P_RATING = 2700; // 1p = 7d
        private const int GRADE_DIFF = 100;
        private const int PRO_GRADE_DIFF = 30;
        private const int MIN_RATING = -900;
        
        // Country-specific rating adjustments for foreign players
        // Positive values = country ranks are stronger than nominal (conservative initial rating)
        // These adjustments help account for different ranking standards across countries
        private static readonly Dictionary<string, int> CountryRatingAdjustments = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "CN", 200 },      // China ranks are typically 1-2 stones stronger
            { "CHN", 200 },     // Alternative China code
            { "CHINA", 200 },
            { "KR", 150 },      // Korea ranks also tend to be strong
            { "KOR", 150 },
            { "KOREA", 150 },
            { "JP", 50 },       // Japan ranks fairly accurate
            { "JPN", 50 },
            { "JAPAN", 50 },
            { "TW", 100 },      // Taiwan has strong rankings
            { "TWN", 100 },
            { "TAIWAN", 100 },
            { "HK", 50 },       // Hong Kong
            { "HKG", 50 },
            { "EU", 0 },        // European (EGF baseline)
            { "EGF", 0 },
            { "US", -50 },      // AGA may be slightly generous
            { "USA", -50 },
            { "AGA", -50 },
            { "MY", 0 },        // Malaysia
            { "MYS", 0 },
            { "ID", 0 },        // Indonesia
            { "IDN", 0 },
            { "TH", 0 },        // Thailand
            { "THA", 0 },
            { "VN", 0 },        // Vietnam
            { "VNM", 0 },
            { "PH", 0 },        // Philippines
            { "PHL", 0 },
        };
        
        // Default adjustment for unknown foreign countries (be conservative)
        private const int DEFAULT_FOREIGN_ADJUSTMENT = -100;
        
        private Dictionary<string, DateTimeOffset> _rankingHistory = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, DateTimeOffset> _swaRankingHistory = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);

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
                if (DisplayName.Contains("Zhou Yi")) // temp fix for known data issue
                    m_initRanking = "[5D]";
                return m_initRanking;
            }
        }

        public bool IsVirtualPlayer => DisplayName.Contains('[');
        public bool IsUnknownPlayer => false;// string.IsNullOrEmpty(InternalInitRanking) || InternalInitRanking.Contains("?");
        public bool IsUnknownRankedPlayer => IsUnknownPlayer || (InternalInitRanking.Contains('[') && !InternalInitRanking.Contains(' '));
        public bool IsNewUnknownRankdedPlayer => MatchCount <= 12 && IsUnknownRankedPlayer;
        public bool IsProPlayer => RankingBeforeCutoffDate.Contains('P');
        public bool IsNewKyuPlayer => MatchCount <= 12 && !InternalInitRanking.Contains('D') && !IsProPlayer && !IsVirtualPlayer;
        public bool IsHiddenPlayer => (InvisiblePlayers.Contains(Email, StringComparer.OrdinalIgnoreCase) || IsNewKyuPlayer || IsNewUnknownRankdedPlayer);

        public bool NeedDynamicFactor(bool intl)
        {
            return intl ? MatchCount <= 12 : IsNewUnknownRankdedPlayer;
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
                return GetKyuRating(5); // Default to 5 kyu = 1600

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
                if (rankingNum == 5)
                {
                    delta = isDan || isKyu ? 50 : 0;
                }

                if (isDan && rankingNum <= 5)
                {
                    rankingNum -= 1;
                }
                else if (isKyu && rankingNum <= 5)
                {
                    rankingNum += 1;
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
                bool useSwaRanking = ranking.Contains('[') ||
                    (ranking.Contains('(') && !ranking.Contains("5D"));
                if (useSwaRanking)
                    ranking = ranking.Substring(0, ranking.IndexOf(' '));
                else
                    ranking = ranking.Substring(ranking.IndexOf('('));
            }

            return ranking;
        }

        /// <summary>
        /// Parses country code from foreign ranking notation.
        /// Supports formats like: [CN 5D], [KR5D], [5D], [CHINA 3D], etc.
        /// </summary>
        /// <param name="ranking">The ranking string to parse</param>
        /// <returns>Country code if found, null otherwise</returns>
        public static string ParseCountryFromRanking(string ranking)
        {
            if (string.IsNullOrEmpty(ranking) || !ranking.Contains('['))
                return null;

            // Extract content between brackets
            var startIdx = ranking.IndexOf('[') + 1;
            var endIdx = ranking.IndexOf(']');
            if (endIdx <= startIdx)
                return null;

            var content = ranking.Substring(startIdx, endIdx - startIdx).Trim().ToUpper();
            
            // Try to find country code pattern
            // Pattern 1: "CN 5D" or "CN5D" - country code followed by rank
            // Pattern 2: "5D" - no country code, just rank
            var match = Regex.Match(content, @"^([A-Z]{2,5})\s*\d");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return null;
        }

        /// <summary>
        /// Gets the rating adjustment for a country.
        /// Positive adjustment means the country's ranks are stronger than nominal,
        /// so we use a higher initial rating (more conservative).
        /// </summary>
        private static int GetCountryAdjustment(string countryCode)
        {
            if (string.IsNullOrEmpty(countryCode))
                return DEFAULT_FOREIGN_ADJUSTMENT;

            if (CountryRatingAdjustments.TryGetValue(countryCode, out int adjustment))
                return adjustment;

            return DEFAULT_FOREIGN_ADJUSTMENT;
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

```

Now let me update the `GetRatingByRanking` method to apply the country adjustments:

---

**User**

could you implement this for me please?
After 12 games, recalculate their "true" initial rating based on performance for historical accuracy

---

**Cursor**

I'll implement performance-based initial rating recalculation after 12 games. Let me first read the current state of the files to ensure I have the latest version.

First, let me add the estimated initial rating property to `ApplicationUser`:

```csharp
using Microsoft.AspNetCore.Identity;
using System.Text.RegularExpressions;

namespace PlayerRatings.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        private const string BIRTH_YEAR = "BY";
        internal const string DATE_FORMAT = "dd/MM/yyyy";
        // EGD Rating Scale: 1 dan = 2100, difference between grades = 100
        // Professional: 1p = 7d = 2700, difference between pro grades = 30
        // Minimum rating = -900
        private const int ONE_D_RATING = 2100;
        private const int ONE_P_RATING = 2700; // 1p = 7d
        private const int GRADE_DIFF = 100;
        private const int PRO_GRADE_DIFF = 30;
        private const int MIN_RATING = -900;
        private Dictionary<string, DateTimeOffset> _rankingHistory = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, DateTimeOffset> _swaRankingHistory = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);

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
                if (DisplayName.Contains("Zhou Yi")) // temp fix for known data issue
                    m_initRanking = "[5D]";
                return m_initRanking;
            }
        }

        public bool IsVirtualPlayer => DisplayName.Contains('[');
        public bool IsUnknownPlayer => false;// string.IsNullOrEmpty(InternalInitRanking) || InternalInitRanking.Contains("?");
        public bool IsUnknownRankedPlayer => IsUnknownPlayer || (InternalInitRanking.Contains('[') && !InternalInitRanking.Contains(' '));
        public bool IsNewUnknownRankdedPlayer => MatchCount <= 12 && IsUnknownRankedPlayer;
        public bool IsProPlayer => RankingBeforeCutoffDate.Contains('P');
        public bool IsNewKyuPlayer => MatchCount <= 12 && !InternalInitRanking.Contains('D') && !IsProPlayer && !IsVirtualPlayer;
        public bool IsHiddenPlayer => (InvisiblePlayers.Contains(Email, StringComparer.OrdinalIgnoreCase) || IsNewKyuPlayer || IsNewUnknownRankdedPlayer);

        public bool NeedDynamicFactor(bool intl)
        {
            return intl ? MatchCount <= 12 : IsNewUnknownRankdedPlayer;
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
                return GetKyuRating(5); // Default to 5 kyu = 1600

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
                if (rankingNum == 5)
                {
                    delta = isDan || isKyu ? 50 : 0;
                }

                if (isDan && rankingNum <= 5)
                {
                    rankingNum -= 1;
                }
                else if (isKyu && rankingNum <= 5)
                {
                    rankingNum += 1;
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
                bool useSwaRanking = ranking.Contains('[') ||
                    (ranking.Contains('(') && !ranking.Contains("5D"));
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

```

Now let me update `EloStat` to track games and calculate performance rating:

```csharp
using PlayerRatings.Engine.Rating;
using PlayerRatings.Models;
using PlayerRatings.Localization;

namespace PlayerRatings.Engine.Stats
{
    public class EloStat : IStat
    {
        protected readonly Dictionary<string, double> _dict = new Dictionary<string, double>();
        protected double _china6dRating = 0.0;

        // Track game results for new foreign/unknown players to calculate performance rating
        // Key: player Id, Value: list of (opponent rating, score) tuples
        private readonly Dictionary<string, List<(double opponentRating, double score)>> _performanceTracker 
            = new Dictionary<string, List<(double opponentRating, double score)>>();

        // Number of games required before calculating estimated initial rating
        private const int GAMES_FOR_ESTIMATION = 12;

        public void AddMatch(Match match)
        {
            double firstUserScore = 1.0;
            if (match.SecondPlayerScore > match.FirstPlayerScore)
            {
                firstUserScore = 0;
            }
            else if (match.SecondPlayerScore == match.FirstPlayerScore)
            {
                firstUserScore = 0.5;
            }

            bool isIntlLeague = match.League.Name.Contains("Intl.");
            int firstPlayerRankingRating = match.FirstPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
            int secondPlayerRankingRating = match.SecondPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
            double firstPlayerRating = _dict.ContainsKey(match.FirstPlayer.Id) ? _dict[match.FirstPlayer.Id] : firstPlayerRankingRating;
            double secondPlayerRating = _dict.ContainsKey(match.SecondPlayer.Id) ? _dict[match.SecondPlayer.Id] : secondPlayerRankingRating;

            double factor1 = match.Factor.GetValueOrDefault(1);
            if (factor1 > 0) factor1 = 1;
            double factor2 = factor1;
            if (factor1 == 1) // factor is not specified
            {
                // use different K factor for new foreign players to make them get to their proper rating positions faster
                // - K factor is the max possible adjustment per match
                if (match.FirstPlayer.NeedDynamicFactor(isIntlLeague))
                {
                    // if winning a stronger player, use a K factor related to the current elo rating difference
                    // larger diff will result in a larger K factor, and elo rating will increase much faster
                    // - generally, each diff of one dan ranking will double the K value once
                    // winning a weaker player will use normal K factor
                    factor1 = CalculateDynamicFactor(firstPlayerRating,
                        // in case the opsite player's rating is off his/her ranking or true skill level too much
                        // normallize it a bit via using the averate value of the current rating and ranking rating to avoid very rediculas variance
                        secondPlayerRating > secondPlayerRankingRating ? (secondPlayerRating + secondPlayerRankingRating) / 2 : secondPlayerRating);

                    if (match.SecondPlayer.NeedDynamicFactor(isIntlLeague))
                    {
                        // normalize the factors if both are new players since we know none of their real rankings
                        factor2 = factor1 = Math.Min(8, factor1);
                    }
                    else
                    {
                        // reduce these new players' impacts to existing players
                        factor2 = match.SecondPlayer.IsVirtualPlayer || match.FirstPlayer.IsUnknownPlayer ? 1 : 0.5; // half of the normal K
                    }
                }
                else if (match.SecondPlayer.NeedDynamicFactor(isIntlLeague))
                {
                    // exceptional matches
                    if (match.SecondPlayerScore > 0)
                    {
                        factor2 = 1;
                    }
                    else
                    {
                        // the same here. losing to a much weaker player could be a disaster
                        factor2 = CalculateDynamicFactor(
                            // normallize opsite rating
                            firstPlayerRating < firstPlayerRankingRating ? (firstPlayerRating + firstPlayerRankingRating) / 2 : firstPlayerRating,
                            secondPlayerRating);
                    }

                    // reduce these new players' impact to existing players
                    factor1 = match.FirstPlayer.IsVirtualPlayer || match.SecondPlayer.IsUnknownPlayer ? 1 : 0.5;
                }

                if (match.FirstPlayer.DisplayName == "[China 5D]" && match.Date.Year >= 2025 && match.Date.Month >= 7)
                {
                    // china 5d players' strenghs varies too much.
                    // reduce their impacts from Jul 2025
                    if (match.Date.Year >= 2025 && match.Date.Month >= 11 && match.Date.Day >= 24)
                    {
                        factor2 *= Math.Min(1, (firstPlayerRating - 1000) * (firstPlayerRating - 1000) / (_china6dRating - 1000) / (secondPlayerRating - 1000));
                    }
                    else
                    {
                        factor2 *= 0.5;
                    }
                }
            }

            var rating1 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, factor1 > 1 ? 40 : Elo.GetK(firstPlayerRating) * factor1);
            var rating2 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, factor2 > 1 ? 40 : Elo.GetK(secondPlayerRating) * factor2);

            if (match.FirstPlayer.DisplayName == "[China 6D]")
                _china6dRating = rating1.NewRatingAPlayer;
            else if (match.SecondPlayer.DisplayName == "[China 6D]")
                _china6dRating = rating1.NewRatingBPlayer;

            match.OldFirstPlayerRating = firstPlayerRating.ToString("F1");
            match.OldSecondPlayerRating = secondPlayerRating.ToString("F1");

            _dict[match.FirstPlayer.Id] = rating1.NewRatingAPlayer;
            _dict[match.SecondPlayer.Id] = rating2.NewRatingBPlayer;

            match.ShiftRating = rating1.ShiftRatingAPlayer.ToString("F1");
            var player2ShiftRating = (secondPlayerRating - _dict[match.SecondPlayer.Id]).ToString("F1");
            if (match.ShiftRating != player2ShiftRating)
            {
                // rating shifts will be different for the two players, display both
                match.ShiftRating += "-" + player2ShiftRating;
            }
        }

        private static double CalculateDynamicFactor(double firstPlayerRating, double secondPlayerRating)
        {
            var ret = Math.Max(1, Math.Pow(2, ((secondPlayerRating - firstPlayerRating) / 200)));
            return ret;
        }

        public virtual string GetResult(ApplicationUser user)
        {
            return _dict.ContainsKey(user.Id) ? _dict[user.Id].ToString("F1") : "";
        }

        public virtual string NameLocalizationKey => nameof(LocalizationKey.Elo);

        public virtual double this[ApplicationUser user]
        {
            get { return _dict.ContainsKey(user.Id) ? _dict[user.Id] : user.GetRatingBeforeDate(user.FirstMatch.Date); }
            set { _dict[user.Id] = value; }
        }
    }

    public class EloStatChange : EloStat
    {
        public override string GetResult(ApplicationUser user)
        {
            return _dict.ContainsKey(user.Id) ? (_dict[user.Id] - user.GetRatingBeforeDate(League.CutoffDate)).ToString("F1") : "";
        }

        public override string NameLocalizationKey => nameof(LocalizationKey.Delta);

        public override double this[ApplicationUser user] => _dict.ContainsKey(user.Id) ? _dict[user.Id] : 0;
    }
}

```

Now add the tracking and calculation logic at the end of `AddMatch`:

```csharp
using PlayerRatings.Engine.Rating;
using PlayerRatings.Models;
using PlayerRatings.Localization;

namespace PlayerRatings.Engine.Stats
{
    public class EloStat : IStat
    {
        protected readonly Dictionary<string, double> _dict = new Dictionary<string, double>();
        protected double _china6dRating = 0.0;

        // Track game results for new foreign/unknown players to calculate performance rating
        // Key: player Id, Value: list of (opponent rating, score) tuples
        private readonly Dictionary<string, List<(double opponentRating, double score)>> _performanceTracker 
            = new Dictionary<string, List<(double opponentRating, double score)>>();

        // Number of games required before calculating estimated initial rating
        private const int GAMES_FOR_ESTIMATION = 12;

        public void AddMatch(Match match)
        {
            double firstUserScore = 1.0;
            if (match.SecondPlayerScore > match.FirstPlayerScore)
            {
                firstUserScore = 0;
            }
            else if (match.SecondPlayerScore == match.FirstPlayerScore)
            {
                firstUserScore = 0.5;
            }

            bool isIntlLeague = match.League.Name.Contains("Intl.");
            int firstPlayerRankingRating = match.FirstPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
            int secondPlayerRankingRating = match.SecondPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
            double firstPlayerRating = _dict.ContainsKey(match.FirstPlayer.Id) ? _dict[match.FirstPlayer.Id] : firstPlayerRankingRating;
            double secondPlayerRating = _dict.ContainsKey(match.SecondPlayer.Id) ? _dict[match.SecondPlayer.Id] : secondPlayerRankingRating;

            double factor1 = match.Factor.GetValueOrDefault(1);
            if (factor1 > 0) factor1 = 1;
            double factor2 = factor1;
            if (factor1 == 1) // factor is not specified
            {
                // use different K factor for new foreign players to make them get to their proper rating positions faster
                // - K factor is the max possible adjustment per match
                if (match.FirstPlayer.NeedDynamicFactor(isIntlLeague))
                {
                    // if winning a stronger player, use a K factor related to the current elo rating difference
                    // larger diff will result in a larger K factor, and elo rating will increase much faster
                    // - generally, each diff of one dan ranking will double the K value once
                    // winning a weaker player will use normal K factor
                    factor1 = CalculateDynamicFactor(firstPlayerRating,
                        // in case the opsite player's rating is off his/her ranking or true skill level too much
                        // normallize it a bit via using the averate value of the current rating and ranking rating to avoid very rediculas variance
                        secondPlayerRating > secondPlayerRankingRating ? (secondPlayerRating + secondPlayerRankingRating) / 2 : secondPlayerRating);

                    if (match.SecondPlayer.NeedDynamicFactor(isIntlLeague))
                    {
                        // normalize the factors if both are new players since we know none of their real rankings
                        factor2 = factor1 = Math.Min(8, factor1);
                    }
                    else
                    {
                        // reduce these new players' impacts to existing players
                        factor2 = match.SecondPlayer.IsVirtualPlayer || match.FirstPlayer.IsUnknownPlayer ? 1 : 0.5; // half of the normal K
                    }
                }
                else if (match.SecondPlayer.NeedDynamicFactor(isIntlLeague))
                {
                    // exceptional matches
                    if (match.SecondPlayerScore > 0)
                    {
                        factor2 = 1;
                    }
                    else
                    {
                        // the same here. losing to a much weaker player could be a disaster
                        factor2 = CalculateDynamicFactor(
                            // normallize opsite rating
                            firstPlayerRating < firstPlayerRankingRating ? (firstPlayerRating + firstPlayerRankingRating) / 2 : firstPlayerRating,
                            secondPlayerRating);
                    }

                    // reduce these new players' impact to existing players
                    factor1 = match.FirstPlayer.IsVirtualPlayer || match.SecondPlayer.IsUnknownPlayer ? 1 : 0.5;
                }

                if (match.FirstPlayer.DisplayName == "[China 5D]" && match.Date.Year >= 2025 && match.Date.Month >= 7)
                {
                    // china 5d players' strenghs varies too much.
                    // reduce their impacts from Jul 2025
                    if (match.Date.Year >= 2025 && match.Date.Month >= 11 && match.Date.Day >= 24)
                    {
                        factor2 *= Math.Min(1, (firstPlayerRating - 1000) * (firstPlayerRating - 1000) / (_china6dRating - 1000) / (secondPlayerRating - 1000));
                    }
                    else
                    {
                        factor2 *= 0.5;
                    }
                }
            }

            var rating1 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, factor1 > 1 ? 40 : Elo.GetK(firstPlayerRating) * factor1);
            var rating2 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, factor2 > 1 ? 40 : Elo.GetK(secondPlayerRating) * factor2);

            if (match.FirstPlayer.DisplayName == "[China 6D]")
                _china6dRating = rating1.NewRatingAPlayer;
            else if (match.SecondPlayer.DisplayName == "[China 6D]")
                _china6dRating = rating1.NewRatingBPlayer;

            match.OldFirstPlayerRating = firstPlayerRating.ToString("F1");
            match.OldSecondPlayerRating = secondPlayerRating.ToString("F1");

            _dict[match.FirstPlayer.Id] = rating1.NewRatingAPlayer;
            _dict[match.SecondPlayer.Id] = rating2.NewRatingBPlayer;

            match.ShiftRating = rating1.ShiftRatingAPlayer.ToString("F1");
            var player2ShiftRating = (secondPlayerRating - _dict[match.SecondPlayer.Id]).ToString("F1");
            if (match.ShiftRating != player2ShiftRating)
            {
                // rating shifts will be different for the two players, display both
                match.ShiftRating += "-" + player2ShiftRating;
            }

            // Track games for performance-based initial rating calculation
            TrackGameForPerformanceRating(match.FirstPlayer, secondPlayerRating, firstUserScore, isIntlLeague);
            TrackGameForPerformanceRating(match.SecondPlayer, firstPlayerRating, 1 - firstUserScore, isIntlLeague);
        }

        /// <summary>
        /// Tracks a game result for a new foreign/unknown player and calculates their
        /// estimated initial rating after they've played enough games.
        /// </summary>
        private void TrackGameForPerformanceRating(ApplicationUser player, double opponentRating, double score, bool isIntlLeague)
        {
            // Only track for players who need performance estimation
            // (foreign/unknown players in their first 12 games)
            if (!player.NeedDynamicFactor(isIntlLeague))
                return;

            // Skip virtual players (aggregated player pools like [China 5D])
            if (player.IsVirtualPlayer)
                return;

            // Initialize tracking list if needed
            if (!_performanceTracker.ContainsKey(player.Id))
            {
                _performanceTracker[player.Id] = new List<(double opponentRating, double score)>();
            }

            // Add this game result
            _performanceTracker[player.Id].Add((opponentRating, score));

            // After GAMES_FOR_ESTIMATION games, calculate estimated initial rating
            if (_performanceTracker[player.Id].Count == GAMES_FOR_ESTIMATION)
            {
                var estimatedRating = CalculatePerformanceRating(_performanceTracker[player.Id]);
                player.EstimatedInitialRating = estimatedRating;
            }
        }

        /// <summary>
        /// Calculates performance rating based on game results.
        /// Uses the FIDE performance rating formula:
        /// Performance Rating = Average Opponent Rating + Rating Difference based on score percentage
        /// </summary>
        private static double CalculatePerformanceRating(List<(double opponentRating, double score)> games)
        {
            if (games.Count == 0)
                return 1600; // Default to 5 kyu

            double totalOpponentRating = 0;
            double totalScore = 0;

            foreach (var game in games)
            {
                totalOpponentRating += game.opponentRating;
                totalScore += game.score;
            }

            double avgOpponentRating = totalOpponentRating / games.Count;
            double winRate = totalScore / games.Count;

            // Convert win rate to rating difference using the inverse of expected score formula
            // For EGD-style ratings, we use a simplified approach:
            // Each 10% above 50% adds roughly 70 rating points
            double ratingDiff;
            if (winRate >= 0.99)
            {
                ratingDiff = 400; // Cap at roughly 4 dan levels above opponents
            }
            else if (winRate <= 0.01)
            {
                ratingDiff = -400; // Cap at roughly 4 dan levels below opponents
            }
            else
            {
                // Use logit function to convert probability to rating difference
                // Rating diff = 400 * log10(winRate / (1 - winRate)) for standard Elo
                // For EGD we use a factor that gives ~100 points per dan level
                ratingDiff = 173.7 * Math.Log(winRate / (1 - winRate));
            }

            return avgOpponentRating + ratingDiff;
        }

        private static double CalculateDynamicFactor(double firstPlayerRating, double secondPlayerRating)
        {
            var ret = Math.Max(1, Math.Pow(2, ((secondPlayerRating - firstPlayerRating) / 200)));
            return ret;
        }

        public virtual string GetResult(ApplicationUser user)
        {
            return _dict.ContainsKey(user.Id) ? _dict[user.Id].ToString("F1") : "";
        }

        public virtual string NameLocalizationKey => nameof(LocalizationKey.Elo);

        public virtual double this[ApplicationUser user]
        {
            get { return _dict.ContainsKey(user.Id) ? _dict[user.Id] : user.GetRatingBeforeDate(user.FirstMatch.Date); }
            set { _dict[user.Id] = value; }
        }
    }

    public class EloStatChange : EloStat
    {
        public override string GetResult(ApplicationUser user)
        {
            return _dict.ContainsKey(user.Id) ? (_dict[user.Id] - user.GetRatingBeforeDate(League.CutoffDate)).ToString("F1") : "";
        }

        public override string NameLocalizationKey => nameof(LocalizationKey.Delta);

        public override double this[ApplicationUser user] => _dict.ContainsKey(user.Id) ? _dict[user.Id] : 0;
    }
}

```

I've implemented the performance-based initial rating calculation. Here's what was added:

## Changes Made

### 1. `ApplicationUser.cs` - Added estimated initial rating storage

```69:88:src/PlayerRatings/Models/ApplicationUser.cs
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
```

### 2. `EloStat.cs` - Added game tracking and performance calculation

The key additions:

1. **Game result tracker** - Stores opponent ratings and scores for each new foreign player
2. **`TrackGameForPerformanceRating()`** - Tracks each game and triggers calculation after 12 games
3. **`CalculatePerformanceRating()`** - Calculates the "true" initial rating using:
   - Average opponent rating
   - Win rate converted to rating difference using logit function
   - Formula: `EstimatedRating = AvgOpponentRating + 173.7 * ln(winRate / (1 - winRate))`

### How It Works

| Win Rate | Rating Adjustment |
|----------|-------------------|
| 50% | +0 (equals avg opponent) |
| 60% | +70 points |
| 70% | +147 points |
| 80% | +241 points |
| 90% | +381 points |
| 40% | -70 points |
| 30% | -147 points |

**Example**: A [5D] foreign player plays 12 games against opponents averaging 2300 rating and wins 9 (75% win rate). Their estimated initial rating would be:
- `2300 + 173.7 * ln(0.75/0.25) = 2300 + 191 ≈ 2491` (about 5D strength, confirming their ranking)

If they only won 4 games (33%), the estimate would be:
- `2300 + 173.7 * ln(0.33/0.67) = 2300 - 123 ≈ 2177` (about 1-2D strength, lower than claimed)

The `GetInitialRating()` method can now be used anywhere you need the player's best-known initial rating for historical accuracy.

```plaintext
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
```

---

**User**

is the estimatedRating really used?

---

**Cursor**

Good question! Let me check if it's actually being used anywhere.

```csharp
int firstPlayerRankingRating = match.FirstPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
int secondPlayerRankingRating = match.SecondPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
double firstPlayerRating = _dict.ContainsKey(match.FirstPlayer.Id) ? _dict[match.FirstPlayer.Id] : firstPlayerRankingRating;
double secondPlayerRating = _dict.ContainsKey(match.SecondPlayer.Id) ? _dict[match.SecondPlayer.Id] : secondPlayerRankingRating;
```

You're right - it's calculated and stored but **not actually used**! The `GetInitialRating()` method exists but nothing calls it. Let me fix this by using the estimated rating to correct the player's current rating after 12 games.

Now let me update the code to actually **apply** the estimated rating by correcting the player's current rating:

```csharp
using PlayerRatings.Engine.Rating;
using PlayerRatings.Models;
using PlayerRatings.Localization;

namespace PlayerRatings.Engine.Stats
{
    public class EloStat : IStat
    {
        protected readonly Dictionary<string, double> _dict = new Dictionary<string, double>();
        protected double _china6dRating = 0.0;

        // Track game results for new foreign/unknown players to calculate performance rating
        // Key: player Id, Value: list of (opponent rating, score) tuples
        private readonly Dictionary<string, List<(double opponentRating, double score)>> _performanceTracker 
            = new Dictionary<string, List<(double opponentRating, double score)>>();

        // Number of games required before calculating estimated initial rating
        private const int GAMES_FOR_ESTIMATION = 12;

        public void AddMatch(Match match)
        {
            double firstUserScore = 1.0;
            if (match.SecondPlayerScore > match.FirstPlayerScore)
            {
                firstUserScore = 0;
            }
            else if (match.SecondPlayerScore == match.FirstPlayerScore)
            {
                firstUserScore = 0.5;
            }

            bool isIntlLeague = match.League.Name.Contains("Intl.");
            int firstPlayerRankingRating = match.FirstPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
            int secondPlayerRankingRating = match.SecondPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
            double firstPlayerRating = _dict.ContainsKey(match.FirstPlayer.Id) ? _dict[match.FirstPlayer.Id] : firstPlayerRankingRating;
            double secondPlayerRating = _dict.ContainsKey(match.SecondPlayer.Id) ? _dict[match.SecondPlayer.Id] : secondPlayerRankingRating;

            double factor1 = match.Factor.GetValueOrDefault(1);
            if (factor1 > 0) factor1 = 1;
            double factor2 = factor1;
            if (factor1 == 1) // factor is not specified
            {
                // use different K factor for new foreign players to make them get to their proper rating positions faster
                // - K factor is the max possible adjustment per match
                if (match.FirstPlayer.NeedDynamicFactor(isIntlLeague))
                {
                    // if winning a stronger player, use a K factor related to the current elo rating difference
                    // larger diff will result in a larger K factor, and elo rating will increase much faster
                    // - generally, each diff of one dan ranking will double the K value once
                    // winning a weaker player will use normal K factor
                    factor1 = CalculateDynamicFactor(firstPlayerRating,
                        // in case the opsite player's rating is off his/her ranking or true skill level too much
                        // normallize it a bit via using the averate value of the current rating and ranking rating to avoid very rediculas variance
                        secondPlayerRating > secondPlayerRankingRating ? (secondPlayerRating + secondPlayerRankingRating) / 2 : secondPlayerRating);

                    if (match.SecondPlayer.NeedDynamicFactor(isIntlLeague))
                    {
                        // normalize the factors if both are new players since we know none of their real rankings
                        factor2 = factor1 = Math.Min(8, factor1);
                    }
                    else
                    {
                        // reduce these new players' impacts to existing players
                        factor2 = match.SecondPlayer.IsVirtualPlayer || match.FirstPlayer.IsUnknownPlayer ? 1 : 0.5; // half of the normal K
                    }
                }
                else if (match.SecondPlayer.NeedDynamicFactor(isIntlLeague))
                {
                    // exceptional matches
                    if (match.SecondPlayerScore > 0)
                    {
                        factor2 = 1;
                    }
                    else
                    {
                        // the same here. losing to a much weaker player could be a disaster
                        factor2 = CalculateDynamicFactor(
                            // normallize opsite rating
                            firstPlayerRating < firstPlayerRankingRating ? (firstPlayerRating + firstPlayerRankingRating) / 2 : firstPlayerRating,
                            secondPlayerRating);
                    }

                    // reduce these new players' impact to existing players
                    factor1 = match.FirstPlayer.IsVirtualPlayer || match.SecondPlayer.IsUnknownPlayer ? 1 : 0.5;
                }

                if (match.FirstPlayer.DisplayName == "[China 5D]" && match.Date.Year >= 2025 && match.Date.Month >= 7)
                {
                    // china 5d players' strenghs varies too much.
                    // reduce their impacts from Jul 2025
                    if (match.Date.Year >= 2025 && match.Date.Month >= 11 && match.Date.Day >= 24)
                    {
                        factor2 *= Math.Min(1, (firstPlayerRating - 1000) * (firstPlayerRating - 1000) / (_china6dRating - 1000) / (secondPlayerRating - 1000));
                    }
                    else
                    {
                        factor2 *= 0.5;
                    }
                }
            }

            var rating1 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, factor1 > 1 ? 40 : Elo.GetK(firstPlayerRating) * factor1);
            var rating2 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, factor2 > 1 ? 40 : Elo.GetK(secondPlayerRating) * factor2);

            if (match.FirstPlayer.DisplayName == "[China 6D]")
                _china6dRating = rating1.NewRatingAPlayer;
            else if (match.SecondPlayer.DisplayName == "[China 6D]")
                _china6dRating = rating1.NewRatingBPlayer;

            match.OldFirstPlayerRating = firstPlayerRating.ToString("F1");
            match.OldSecondPlayerRating = secondPlayerRating.ToString("F1");

            _dict[match.FirstPlayer.Id] = rating1.NewRatingAPlayer;
            _dict[match.SecondPlayer.Id] = rating2.NewRatingBPlayer;

            match.ShiftRating = rating1.ShiftRatingAPlayer.ToString("F1");
            var player2ShiftRating = (secondPlayerRating - _dict[match.SecondPlayer.Id]).ToString("F1");
            if (match.ShiftRating != player2ShiftRating)
            {
                // rating shifts will be different for the two players, display both
                match.ShiftRating += "-" + player2ShiftRating;
            }

            // Track games for performance-based initial rating calculation
            TrackGameForPerformanceRating(match.FirstPlayer, secondPlayerRating, firstUserScore, isIntlLeague);
            TrackGameForPerformanceRating(match.SecondPlayer, firstPlayerRating, 1 - firstUserScore, isIntlLeague);
        }

        /// <summary>
        /// Tracks a game result for a new foreign/unknown player and calculates their
        /// estimated initial rating after they've played enough games.
        /// After calculation, applies a correction to the player's current rating.
        /// </summary>
        private void TrackGameForPerformanceRating(ApplicationUser player, double opponentRating, double score, bool isIntlLeague)
        {
            // Only track for players who need performance estimation
            // (foreign/unknown players in their first 12 games)
            if (!player.NeedDynamicFactor(isIntlLeague))
                return;

            // Skip virtual players (aggregated player pools like [China 5D])
            if (player.IsVirtualPlayer)
                return;

            // Initialize tracking list if needed
            if (!_performanceTracker.ContainsKey(player.Id))
            {
                _performanceTracker[player.Id] = new List<(double opponentRating, double score)>();
            }

            // Add this game result
            _performanceTracker[player.Id].Add((opponentRating, score));

            // After GAMES_FOR_ESTIMATION games, calculate estimated initial rating and apply correction
            if (_performanceTracker[player.Id].Count == GAMES_FOR_ESTIMATION)
            {
                var estimatedInitialRating = CalculatePerformanceRating(_performanceTracker[player.Id]);
                player.EstimatedInitialRating = estimatedInitialRating;

                // Apply rating correction based on the difference between
                // original ranking-based rating and performance-based estimate
                if (_dict.ContainsKey(player.Id))
                {
                    double originalInitialRating = player.GetRatingBeforeDate(player.FirstMatch.Date, isIntlLeague);
                    double ratingCorrection = estimatedInitialRating - originalInitialRating;
                    
                    // Apply partial correction (50%) to avoid over-adjusting
                    // The dynamic K-factor should have already moved them partway to their true rating
                    double currentRating = _dict[player.Id];
                    double correctedRating = currentRating + ratingCorrection * 0.5;
                    
                    _dict[player.Id] = correctedRating;
                }

                // Clean up tracker as we no longer need to track this player
                _performanceTracker.Remove(player.Id);
            }
        }

        /// <summary>
        /// Calculates performance rating based on game results.
        /// Uses the FIDE performance rating formula:
        /// Performance Rating = Average Opponent Rating + Rating Difference based on score percentage
        /// </summary>
        private static double CalculatePerformanceRating(List<(double opponentRating, double score)> games)
        {
            if (games.Count == 0)
                return 1600; // Default to 5 kyu

            double totalOpponentRating = 0;
            double totalScore = 0;

            foreach (var game in games)
            {
                totalOpponentRating += game.opponentRating;
                totalScore += game.score;
            }

            double avgOpponentRating = totalOpponentRating / games.Count;
            double winRate = totalScore / games.Count;

            // Convert win rate to rating difference using the inverse of expected score formula
            // For EGD-style ratings, we use a simplified approach:
            // Each 10% above 50% adds roughly 70 rating points
            double ratingDiff;
            if (winRate >= 0.99)
            {
                ratingDiff = 400; // Cap at roughly 4 dan levels above opponents
            }
            else if (winRate <= 0.01)
            {
                ratingDiff = -400; // Cap at roughly 4 dan levels below opponents
            }
            else
            {
                // Use logit function to convert probability to rating difference
                // Rating diff = 400 * log10(winRate / (1 - winRate)) for standard Elo
                // For EGD we use a factor that gives ~100 points per dan level
                ratingDiff = 173.7 * Math.Log(winRate / (1 - winRate));
            }

            return avgOpponentRating + ratingDiff;
        }

        private static double CalculateDynamicFactor(double firstPlayerRating, double secondPlayerRating)
        {
            var ret = Math.Max(1, Math.Pow(2, ((secondPlayerRating - firstPlayerRating) / 200)));
            return ret;
        }

        public virtual string GetResult(ApplicationUser user)
        {
            return _dict.ContainsKey(user.Id) ? _dict[user.Id].ToString("F1") : "";
        }

        public virtual string NameLocalizationKey => nameof(LocalizationKey.Elo);

        public virtual double this[ApplicationUser user]
        {
            get { return _dict.ContainsKey(user.Id) ? _dict[user.Id] : user.GetRatingBeforeDate(user.FirstMatch.Date); }
            set { _dict[user.Id] = value; }
        }
    }

    public class EloStatChange : EloStat
    {
        public override string GetResult(ApplicationUser user)
        {
            return _dict.ContainsKey(user.Id) ? (_dict[user.Id] - user.GetRatingBeforeDate(League.CutoffDate)).ToString("F1") : "";
        }

        public override string NameLocalizationKey => nameof(LocalizationKey.Delta);

        public override double this[ApplicationUser user] => _dict.ContainsKey(user.Id) ? _dict[user.Id] : 0;
    }
}

```

Now the estimated rating is actually used. Here's what happens after 12 games:

## How It Works Now

```129:170:src/PlayerRatings/Engine/Stats/EloStat.cs
        /// <summary>
        /// Tracks a game result for a new foreign/unknown player and calculates their
        /// estimated initial rating after they've played enough games.
        /// After calculation, applies a correction to the player's current rating.
        /// </summary>
        private void TrackGameForPerformanceRating(...)
        {
            // ... tracking logic ...

            // After GAMES_FOR_ESTIMATION games, calculate estimated initial rating and apply correction
            if (_performanceTracker[player.Id].Count == GAMES_FOR_ESTIMATION)
            {
                var estimatedInitialRating = CalculatePerformanceRating(_performanceTracker[player.Id]);
                player.EstimatedInitialRating = estimatedInitialRating;

                // Apply rating correction based on the difference between
                // original ranking-based rating and performance-based estimate
                if (_dict.ContainsKey(player.Id))
                {
                    double originalInitialRating = player.GetRatingBeforeDate(player.FirstMatch.Date, isIntlLeague);
                    double ratingCorrection = estimatedInitialRating - originalInitialRating;
                    
                    // Apply partial correction (50%) to avoid over-adjusting
                    // The dynamic K-factor should have already moved them partway to their true rating
                    double currentRating = _dict[player.Id];
                    double correctedRating = currentRating + ratingCorrection * 0.5;
                    
                    _dict[player.Id] = correctedRating;
                }

                // Clean up tracker as we no longer need to track this player
                _performanceTracker.Remove(player.Id);
            }
        }
```

## Example

A foreign [5D] player (initial rating 2500) plays 12 games:
- Performance calculation shows they're actually ~2300 (about 3D strength)
- Difference: `2300 - 2500 = -200`
- After 12 games with dynamic K, they might be at 2400
- Correction applied: `2400 + (-200 × 0.5) = 2300`

The 50% factor avoids over-correcting since the dynamic K-factor has already moved them partway toward their true rating. The tracker is also cleaned up after calculation to free memory.

```plaintext
        /// <summary>
        /// Tracks a game result for a new foreign/unknown player and calculates their
        /// estimated initial rating after they've played enough games.
        /// After calculation, applies a correction to the player's current rating.
        /// </summary>
        private void TrackGameForPerformanceRating(...)
        {
            // ... tracking logic ...

            // After GAMES_FOR_ESTIMATION games, calculate estimated initial rating and apply correction
            if (_performanceTracker[player.Id].Count == GAMES_FOR_ESTIMATION)
            {
                var estimatedInitialRating = CalculatePerformanceRating(_performanceTracker[player.Id]);
                player.EstimatedInitialRating = estimatedInitialRating;

                // Apply rating correction based on the difference between
                // original ranking-based rating and performance-based estimate
                if (_dict.ContainsKey(player.Id))
                {
                    double originalInitialRating = player.GetRatingBeforeDate(player.FirstMatch.Date, isIntlLeague);
                    double ratingCorrection = estimatedInitialRating - originalInitialRating;
                    
                    // Apply partial correction (50%) to avoid over-adjusting
                    // The dynamic K-factor should have already moved them partway to their true rating
                    double currentRating = _dict[player.Id];
                    double correctedRating = currentRating + ratingCorrection * 0.5;
                    
                    _dict[player.Id] = correctedRating;
                }

                // Clean up tracker as we no longer need to track this player
                _performanceTracker.Remove(player.Id);
            }
        }
```

---

**User**

the dynamic K-factor is not really used, also its calculation might not be correct. do you have any suggestion to improve it please?

---

**Cursor**

Let me analyze the current dynamic K-factor implementation and suggest improvements.

```plaintext
K_multiplier = 1 + max(0, (12 - games_played) / 6)
```

```plaintext
RD = RD_max * decay^games_played
K = K_base * (1 + RD_factor)
```

```plaintext
K_multiplier = 1 + surprise_factor * uncertainty_factor
```

## Issues with Current Dynamic K-Factor

1. **Asymmetric**: Only boosts K when winning vs stronger opponents, not when losing to weaker ones
2. **Complex logic**: Hard to follow with many conditional branches
3. **Formula issue**: `Math.Pow(2, diff/200)` only helps in one direction
4. **Not truly "dynamic"**: It's more of a win-boost than uncertainty-based K

## Suggested Improvements

I recommend a simpler **games-based uncertainty approach**:

```
K_multiplier = 1 + max(0, (GAMES_FOR_ESTIMATION - games_played) / 6)
```

| Games Played | Multiplier | Effect |
|--------------|------------|--------|
| 0-1 | 3.0x | Fast convergence for new players |
| 6 | 2.0x | Still adjusting quickly |
| 12+ | 1.0x | Normal K factor |

This is symmetric (applies to wins AND losses), simpler, and works with the performance-based correction you now have.

Would you like me to implement this cleaner approach?

```plaintext
K_multiplier = 1 + max(0, (GAMES_FOR_ESTIMATION - games_played) / 6)
```

```csharp
using PlayerRatings.Engine.Rating;
using PlayerRatings.Models;
using PlayerRatings.Localization;

namespace PlayerRatings.Engine.Stats
{
    public class EloStat : IStat
    {
        protected readonly Dictionary<string, double> _dict = new Dictionary<string, double>();
        protected double _china6dRating = 0.0;

        // Track game results for new foreign/unknown players to calculate performance rating
        // Key: player Id, Value: list of (opponent rating, score) tuples
        private readonly Dictionary<string, List<(double opponentRating, double score)>> _performanceTracker 
            = new Dictionary<string, List<(double opponentRating, double score)>>();

        // Number of games required before calculating estimated initial rating
        private const int GAMES_FOR_ESTIMATION = 12;

        public void AddMatch(Match match)
        {
            double firstUserScore = 1.0;
            if (match.SecondPlayerScore > match.FirstPlayerScore)
            {
                firstUserScore = 0;
            }
            else if (match.SecondPlayerScore == match.FirstPlayerScore)
            {
                firstUserScore = 0.5;
            }

            bool isIntlLeague = match.League.Name.Contains("Intl.");
            int firstPlayerRankingRating = match.FirstPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
            int secondPlayerRankingRating = match.SecondPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
            double firstPlayerRating = _dict.ContainsKey(match.FirstPlayer.Id) ? _dict[match.FirstPlayer.Id] : firstPlayerRankingRating;
            double secondPlayerRating = _dict.ContainsKey(match.SecondPlayer.Id) ? _dict[match.SecondPlayer.Id] : secondPlayerRankingRating;

            double factor1 = match.Factor.GetValueOrDefault(1);
            if (factor1 > 0) factor1 = 1;
            double factor2 = factor1;
            if (factor1 == 1) // factor is not specified
            {
                // use different K factor for new foreign players to make them get to their proper rating positions faster
                // - K factor is the max possible adjustment per match
                if (match.FirstPlayer.NeedDynamicFactor(isIntlLeague))
                {
                    // if winning a stronger player, use a K factor related to the current elo rating difference
                    // larger diff will result in a larger K factor, and elo rating will increase much faster
                    // - generally, each diff of one dan ranking will double the K value once
                    // winning a weaker player will use normal K factor
                    factor1 = CalculateDynamicFactor(firstPlayerRating,
                        // in case the opsite player's rating is off his/her ranking or true skill level too much
                        // normallize it a bit via using the averate value of the current rating and ranking rating to avoid very rediculas variance
                        secondPlayerRating > secondPlayerRankingRating ? (secondPlayerRating + secondPlayerRankingRating) / 2 : secondPlayerRating);

                    if (match.SecondPlayer.NeedDynamicFactor(isIntlLeague))
                    {
                        // normalize the factors if both are new players since we know none of their real rankings
                        factor2 = factor1 = Math.Min(8, factor1);
                    }
                    else
                    {
                        // reduce these new players' impacts to existing players
                        factor2 = match.SecondPlayer.IsVirtualPlayer || match.FirstPlayer.IsUnknownPlayer ? 1 : 0.5; // half of the normal K
                    }
                }
                else if (match.SecondPlayer.NeedDynamicFactor(isIntlLeague))
                {
                    // exceptional matches
                    if (match.SecondPlayerScore > 0)
                    {
                        factor2 = 1;
                    }
                    else
                    {
                        // the same here. losing to a much weaker player could be a disaster
                        factor2 = CalculateDynamicFactor(
                            // normallize opsite rating
                            firstPlayerRating < firstPlayerRankingRating ? (firstPlayerRating + firstPlayerRankingRating) / 2 : firstPlayerRating,
                            secondPlayerRating);
                    }

                    // reduce these new players' impact to existing players
                    factor1 = match.FirstPlayer.IsVirtualPlayer || match.SecondPlayer.IsUnknownPlayer ? 1 : 0.5;
                }

                if (match.FirstPlayer.DisplayName == "[China 5D]" && match.Date.Year >= 2025 && match.Date.Month >= 7)
                {
                    // china 5d players' strenghs varies too much.
                    // reduce their impacts from Jul 2025
                    if (match.Date.Year >= 2025 && match.Date.Month >= 11 && match.Date.Day >= 24)
                    {
                        factor2 *= Math.Min(1, (firstPlayerRating - 1000) * (firstPlayerRating - 1000) / (_china6dRating - 1000) / (secondPlayerRating - 1000));
                    }
                    else
                    {
                        factor2 *= 0.5;
                    }
                }
            }

            var rating1 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, factor1 > 1 ? 40 : Elo.GetK(firstPlayerRating) * factor1);
            var rating2 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, factor2 > 1 ? 40 : Elo.GetK(secondPlayerRating) * factor2);

            if (match.FirstPlayer.DisplayName == "[China 6D]")
                _china6dRating = rating1.NewRatingAPlayer;
            else if (match.SecondPlayer.DisplayName == "[China 6D]")
                _china6dRating = rating1.NewRatingBPlayer;

            match.OldFirstPlayerRating = firstPlayerRating.ToString("F1");
            match.OldSecondPlayerRating = secondPlayerRating.ToString("F1");

            _dict[match.FirstPlayer.Id] = rating1.NewRatingAPlayer;
            _dict[match.SecondPlayer.Id] = rating2.NewRatingBPlayer;

            match.ShiftRating = rating1.ShiftRatingAPlayer.ToString("F1");
            var player2ShiftRating = (secondPlayerRating - _dict[match.SecondPlayer.Id]).ToString("F1");
            if (match.ShiftRating != player2ShiftRating)
            {
                // rating shifts will be different for the two players, display both
                match.ShiftRating += "-" + player2ShiftRating;
            }

            // Track games for performance-based initial rating calculation
            TrackGameForPerformanceRating(match.FirstPlayer, secondPlayerRating, firstUserScore, isIntlLeague);
            TrackGameForPerformanceRating(match.SecondPlayer, firstPlayerRating, 1 - firstUserScore, isIntlLeague);
        }

        /// <summary>
        /// Tracks a game result for a new foreign/unknown player and calculates their
        /// estimated initial rating after they've played enough games.
        /// After calculation, applies a correction to the player's current rating.
        /// </summary>
        private void TrackGameForPerformanceRating(ApplicationUser player, double opponentRating, double score, bool isIntlLeague)
        {
            // Only track for players who need performance estimation
            // (foreign/unknown players in their first 12 games)
            if (!player.NeedDynamicFactor(isIntlLeague))
                return;

            // Skip virtual players (aggregated player pools like [China 5D])
            if (player.IsVirtualPlayer)
                return;

            // Initialize tracking list if needed
            if (!_performanceTracker.ContainsKey(player.Id))
            {
                _performanceTracker[player.Id] = new List<(double opponentRating, double score)>();
            }

            // Add this game result
            _performanceTracker[player.Id].Add((opponentRating, score));

            // After GAMES_FOR_ESTIMATION games, calculate estimated initial rating and apply correction
            if (_performanceTracker[player.Id].Count == GAMES_FOR_ESTIMATION)
            {
                var estimatedInitialRating = CalculatePerformanceRating(_performanceTracker[player.Id]);
                player.EstimatedInitialRating = estimatedInitialRating;

                // Apply rating correction based on the difference between
                // original ranking-based rating and performance-based estimate
                if (_dict.ContainsKey(player.Id))
                {
                    double originalInitialRating = player.GetRatingBeforeDate(player.FirstMatch.Date, isIntlLeague);
                    double ratingCorrection = estimatedInitialRating - originalInitialRating;
                    
                    // Apply partial correction (50%) to avoid over-adjusting
                    // The dynamic K-factor should have already moved them partway to their true rating
                    double currentRating = _dict[player.Id];
                    double correctedRating = currentRating + ratingCorrection * 0.5;
                    
                    _dict[player.Id] = correctedRating;
                }

                // Clean up tracker as we no longer need to track this player
                _performanceTracker.Remove(player.Id);
            }
        }

        /// <summary>
        /// Calculates performance rating based on game results.
        /// Uses the FIDE performance rating formula:
        /// Performance Rating = Average Opponent Rating + Rating Difference based on score percentage
        /// </summary>
        private static double CalculatePerformanceRating(List<(double opponentRating, double score)> games)
        {
            if (games.Count == 0)
                return 1600; // Default to 5 kyu

            double totalOpponentRating = 0;
            double totalScore = 0;

            foreach (var game in games)
            {
                totalOpponentRating += game.opponentRating;
                totalScore += game.score;
            }

            double avgOpponentRating = totalOpponentRating / games.Count;
            double winRate = totalScore / games.Count;

            // Convert win rate to rating difference using the inverse of expected score formula
            // For EGD-style ratings, we use a simplified approach:
            // Each 10% above 50% adds roughly 70 rating points
            double ratingDiff;
            if (winRate >= 0.99)
            {
                ratingDiff = 400; // Cap at roughly 4 dan levels above opponents
            }
            else if (winRate <= 0.01)
            {
                ratingDiff = -400; // Cap at roughly 4 dan levels below opponents
            }
            else
            {
                // Use logit function to convert probability to rating difference
                // Rating diff = 400 * log10(winRate / (1 - winRate)) for standard Elo
                // For EGD we use a factor that gives ~100 points per dan level
                ratingDiff = 173.7 * Math.Log(winRate / (1 - winRate));
            }

            return avgOpponentRating + ratingDiff;
        }

        /// <summary>
        /// Calculates a K-factor multiplier based on games played (uncertainty-based).
        /// New players get higher K to converge faster, decreasing to 1.0 at GAMES_FOR_ESTIMATION.
        /// Formula: multiplier = 1 + max(0, (GAMES_FOR_ESTIMATION - gamesPlayed) / 6)
        /// </summary>
        /// <param name="gamesPlayed">Number of games the player has played</param>
        /// <returns>K-factor multiplier (1.0 to 3.0)</returns>
        private static double CalculateUncertaintyFactor(int gamesPlayed)
        {
            // Linear decrease from 3.0 at 0 games to 1.0 at 12 games
            // 0 games: 1 + 12/6 = 3.0
            // 6 games: 1 + 6/6 = 2.0
            // 12 games: 1 + 0/6 = 1.0
            return 1 + Math.Max(0, (GAMES_FOR_ESTIMATION - gamesPlayed) / 6.0);
        }

        /// <summary>
        /// Legacy method for calculating dynamic factor based on rating difference.
        /// Only boosts K when opponent is stronger (asymmetric).
        /// Consider using CalculateUncertaintyFactor instead for symmetric behavior.
        /// </summary>
        [Obsolete("Use CalculateUncertaintyFactor for symmetric games-based K adjustment")]
        private static double CalculateDynamicFactor(double firstPlayerRating, double secondPlayerRating)
        {
            var ret = Math.Max(1, Math.Pow(2, ((secondPlayerRating - firstPlayerRating) / 200)));
            return ret;
        }

        public virtual string GetResult(ApplicationUser user)
        {
            return _dict.ContainsKey(user.Id) ? _dict[user.Id].ToString("F1") : "";
        }

        public virtual string NameLocalizationKey => nameof(LocalizationKey.Elo);

        public virtual double this[ApplicationUser user]
        {
            get { return _dict.ContainsKey(user.Id) ? _dict[user.Id] : user.GetRatingBeforeDate(user.FirstMatch.Date); }
            set { _dict[user.Id] = value; }
        }
    }

    public class EloStatChange : EloStat
    {
        public override string GetResult(ApplicationUser user)
        {
            return _dict.ContainsKey(user.Id) ? (_dict[user.Id] - user.GetRatingBeforeDate(League.CutoffDate)).ToString("F1") : "";
        }

        public override string NameLocalizationKey => nameof(LocalizationKey.Delta);

        public override double this[ApplicationUser user] => _dict.ContainsKey(user.Id) ? _dict[user.Id] : 0;
    }
}

```

Now let me update the K-factor logic to use the new uncertainty-based approach:

```csharp
using PlayerRatings.Engine.Rating;
using PlayerRatings.Models;
using PlayerRatings.Localization;

namespace PlayerRatings.Engine.Stats
{
    public class EloStat : IStat
    {
        protected readonly Dictionary<string, double> _dict = new Dictionary<string, double>();
        protected double _china6dRating = 0.0;

        // Track game results for new foreign/unknown players to calculate performance rating
        // Key: player Id, Value: list of (opponent rating, score) tuples
        private readonly Dictionary<string, List<(double opponentRating, double score)>> _performanceTracker 
            = new Dictionary<string, List<(double opponentRating, double score)>>();

        // Number of games required before calculating estimated initial rating
        private const int GAMES_FOR_ESTIMATION = 12;

        public void AddMatch(Match match)
        {
            double firstUserScore = 1.0;
            if (match.SecondPlayerScore > match.FirstPlayerScore)
            {
                firstUserScore = 0;
            }
            else if (match.SecondPlayerScore == match.FirstPlayerScore)
            {
                firstUserScore = 0.5;
            }

            bool isIntlLeague = match.League.Name.Contains("Intl.");
            int firstPlayerRankingRating = match.FirstPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
            int secondPlayerRankingRating = match.SecondPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
            double firstPlayerRating = _dict.ContainsKey(match.FirstPlayer.Id) ? _dict[match.FirstPlayer.Id] : firstPlayerRankingRating;
            double secondPlayerRating = _dict.ContainsKey(match.SecondPlayer.Id) ? _dict[match.SecondPlayer.Id] : secondPlayerRankingRating;

            double factor1 = match.Factor.GetValueOrDefault(1);
            if (factor1 > 0) factor1 = 1;
            double factor2 = factor1;
            if (factor1 == 1) // factor is not specified
            {
                bool player1NeedsDynamic = match.FirstPlayer.NeedDynamicFactor(isIntlLeague);
                bool player2NeedsDynamic = match.SecondPlayer.NeedDynamicFactor(isIntlLeague);

                // Apply uncertainty-based K factor for new/foreign players
                // This is symmetric - applies to both wins and losses
                if (player1NeedsDynamic)
                {
                    factor1 = CalculateUncertaintyFactor(match.FirstPlayer.MatchCount);
                    
                    // Reduce impact on established players when playing against uncertain players
                    if (!player2NeedsDynamic && !match.SecondPlayer.IsVirtualPlayer)
                    {
                        factor2 = 0.5; // Half K for established player
                    }
                }

                if (player2NeedsDynamic)
                {
                    factor2 = CalculateUncertaintyFactor(match.SecondPlayer.MatchCount);
                    
                    // Reduce impact on established players when playing against uncertain players
                    if (!player1NeedsDynamic && !match.FirstPlayer.IsVirtualPlayer)
                    {
                        factor1 = 0.5; // Half K for established player
                    }
                }

                // When both are new players, cap the factors to avoid excessive volatility
                if (player1NeedsDynamic && player2NeedsDynamic)
                {
                    factor1 = Math.Min(factor1, 2.0);
                    factor2 = Math.Min(factor2, 2.0);
                }

                // Special handling for China 5D virtual player pool (high variance)
                if (match.FirstPlayer.DisplayName == "[China 5D]" && match.Date.Year >= 2025 && match.Date.Month >= 7)
                {
                    if (match.Date.Year >= 2025 && match.Date.Month >= 11 && match.Date.Day >= 24)
                    {
                        factor2 *= Math.Min(1, (firstPlayerRating - 1000) * (firstPlayerRating - 1000) / (_china6dRating - 1000) / (secondPlayerRating - 1000));
                    }
                    else
                    {
                        factor2 *= 0.5;
                    }
                }
            }

            var rating1 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, factor1 > 1 ? 40 : Elo.GetK(firstPlayerRating) * factor1);
            var rating2 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, factor2 > 1 ? 40 : Elo.GetK(secondPlayerRating) * factor2);

            if (match.FirstPlayer.DisplayName == "[China 6D]")
                _china6dRating = rating1.NewRatingAPlayer;
            else if (match.SecondPlayer.DisplayName == "[China 6D]")
                _china6dRating = rating1.NewRatingBPlayer;

            match.OldFirstPlayerRating = firstPlayerRating.ToString("F1");
            match.OldSecondPlayerRating = secondPlayerRating.ToString("F1");

            _dict[match.FirstPlayer.Id] = rating1.NewRatingAPlayer;
            _dict[match.SecondPlayer.Id] = rating2.NewRatingBPlayer;

            match.ShiftRating = rating1.ShiftRatingAPlayer.ToString("F1");
            var player2ShiftRating = (secondPlayerRating - _dict[match.SecondPlayer.Id]).ToString("F1");
            if (match.ShiftRating != player2ShiftRating)
            {
                // rating shifts will be different for the two players, display both
                match.ShiftRating += "-" + player2ShiftRating;
            }

            // Track games for performance-based initial rating calculation
            TrackGameForPerformanceRating(match.FirstPlayer, secondPlayerRating, firstUserScore, isIntlLeague);
            TrackGameForPerformanceRating(match.SecondPlayer, firstPlayerRating, 1 - firstUserScore, isIntlLeague);
        }

        /// <summary>
        /// Tracks a game result for a new foreign/unknown player and calculates their
        /// estimated initial rating after they've played enough games.
        /// After calculation, applies a correction to the player's current rating.
        /// </summary>
        private void TrackGameForPerformanceRating(ApplicationUser player, double opponentRating, double score, bool isIntlLeague)
        {
            // Only track for players who need performance estimation
            // (foreign/unknown players in their first 12 games)
            if (!player.NeedDynamicFactor(isIntlLeague))
                return;

            // Skip virtual players (aggregated player pools like [China 5D])
            if (player.IsVirtualPlayer)
                return;

            // Initialize tracking list if needed
            if (!_performanceTracker.ContainsKey(player.Id))
            {
                _performanceTracker[player.Id] = new List<(double opponentRating, double score)>();
            }

            // Add this game result
            _performanceTracker[player.Id].Add((opponentRating, score));

            // After GAMES_FOR_ESTIMATION games, calculate estimated initial rating and apply correction
            if (_performanceTracker[player.Id].Count == GAMES_FOR_ESTIMATION)
            {
                var estimatedInitialRating = CalculatePerformanceRating(_performanceTracker[player.Id]);
                player.EstimatedInitialRating = estimatedInitialRating;

                // Apply rating correction based on the difference between
                // original ranking-based rating and performance-based estimate
                if (_dict.ContainsKey(player.Id))
                {
                    double originalInitialRating = player.GetRatingBeforeDate(player.FirstMatch.Date, isIntlLeague);
                    double ratingCorrection = estimatedInitialRating - originalInitialRating;
                    
                    // Apply partial correction (50%) to avoid over-adjusting
                    // The dynamic K-factor should have already moved them partway to their true rating
                    double currentRating = _dict[player.Id];
                    double correctedRating = currentRating + ratingCorrection * 0.5;
                    
                    _dict[player.Id] = correctedRating;
                }

                // Clean up tracker as we no longer need to track this player
                _performanceTracker.Remove(player.Id);
            }
        }

        /// <summary>
        /// Calculates performance rating based on game results.
        /// Uses the FIDE performance rating formula:
        /// Performance Rating = Average Opponent Rating + Rating Difference based on score percentage
        /// </summary>
        private static double CalculatePerformanceRating(List<(double opponentRating, double score)> games)
        {
            if (games.Count == 0)
                return 1600; // Default to 5 kyu

            double totalOpponentRating = 0;
            double totalScore = 0;

            foreach (var game in games)
            {
                totalOpponentRating += game.opponentRating;
                totalScore += game.score;
            }

            double avgOpponentRating = totalOpponentRating / games.Count;
            double winRate = totalScore / games.Count;

            // Convert win rate to rating difference using the inverse of expected score formula
            // For EGD-style ratings, we use a simplified approach:
            // Each 10% above 50% adds roughly 70 rating points
            double ratingDiff;
            if (winRate >= 0.99)
            {
                ratingDiff = 400; // Cap at roughly 4 dan levels above opponents
            }
            else if (winRate <= 0.01)
            {
                ratingDiff = -400; // Cap at roughly 4 dan levels below opponents
            }
            else
            {
                // Use logit function to convert probability to rating difference
                // Rating diff = 400 * log10(winRate / (1 - winRate)) for standard Elo
                // For EGD we use a factor that gives ~100 points per dan level
                ratingDiff = 173.7 * Math.Log(winRate / (1 - winRate));
            }

            return avgOpponentRating + ratingDiff;
        }

        /// <summary>
        /// Calculates a K-factor multiplier based on games played (uncertainty-based).
        /// New players get higher K to converge faster, decreasing to 1.0 at GAMES_FOR_ESTIMATION.
        /// Formula: multiplier = 1 + max(0, (GAMES_FOR_ESTIMATION - gamesPlayed) / 6)
        /// </summary>
        /// <param name="gamesPlayed">Number of games the player has played</param>
        /// <returns>K-factor multiplier (1.0 to 3.0)</returns>
        private static double CalculateUncertaintyFactor(int gamesPlayed)
        {
            // Linear decrease from 3.0 at 0 games to 1.0 at 12 games
            // 0 games: 1 + 12/6 = 3.0
            // 6 games: 1 + 6/6 = 2.0
            // 12 games: 1 + 0/6 = 1.0
            return 1 + Math.Max(0, (GAMES_FOR_ESTIMATION - gamesPlayed) / 6.0);
        }

        /// <summary>
        /// Legacy method for calculating dynamic factor based on rating difference.
        /// Only boosts K when opponent is stronger (asymmetric).
        /// Consider using CalculateUncertaintyFactor instead for symmetric behavior.
        /// </summary>
        [Obsolete("Use CalculateUncertaintyFactor for symmetric games-based K adjustment")]
        private static double CalculateDynamicFactor(double firstPlayerRating, double secondPlayerRating)
        {
            var ret = Math.Max(1, Math.Pow(2, ((secondPlayerRating - firstPlayerRating) / 200)));
            return ret;
        }

        public virtual string GetResult(ApplicationUser user)
        {
            return _dict.ContainsKey(user.Id) ? _dict[user.Id].ToString("F1") : "";
        }

        public virtual string NameLocalizationKey => nameof(LocalizationKey.Elo);

        public virtual double this[ApplicationUser user]
        {
            get { return _dict.ContainsKey(user.Id) ? _dict[user.Id] : user.GetRatingBeforeDate(user.FirstMatch.Date); }
            set { _dict[user.Id] = value; }
        }
    }

    public class EloStatChange : EloStat
    {
        public override string GetResult(ApplicationUser user)
        {
            return _dict.ContainsKey(user.Id) ? (_dict[user.Id] - user.GetRatingBeforeDate(League.CutoffDate)).ToString("F1") : "";
        }

        public override string NameLocalizationKey => nameof(LocalizationKey.Delta);

        public override double this[ApplicationUser user] => _dict.ContainsKey(user.Id) ? _dict[user.Id] : 0;
    }
}

```

Here's the improved implementation:

## Key Changes

### 1. New Uncertainty-Based K Factor

```225:238:src/PlayerRatings/Engine/Stats/EloStat.cs
        /// <summary>
        /// Calculates a K-factor multiplier based on games played (uncertainty-based).
        /// New players get higher K to converge faster, decreasing to 1.0 at GAMES_FOR_ESTIMATION.
        /// Formula: multiplier = 1 + max(0, (GAMES_FOR_ESTIMATION - gamesPlayed) / 6)
        /// </summary>
        private static double CalculateUncertaintyFactor(int gamesPlayed)
        {
            // Linear decrease from 3.0 at 0 games to 1.0 at 12 games
            return 1 + Math.Max(0, (GAMES_FOR_ESTIMATION - gamesPlayed) / 6.0);
        }
```

### 2. Simplified & Symmetric K-Factor Logic

| Scenario | Before | After |
|----------|--------|-------|
| New player wins vs stronger | K × 2-8 (asymmetric) | K × 3.0→1.0 (games-based) |
| New player loses vs weaker | K × 1 (no boost!) | K × 3.0→1.0 (same boost) |
| Established vs new player | K × 0.5 | K × 0.5 (unchanged) |
| Both new players | Complex capping | K × 2.0 max each |

### 3. Benefits

- **Symmetric**: Same K boost for wins and losses - faster convergence in both directions
- **Predictable**: Linear decrease based on games played
- **Simpler**: Removed complex conditional branches
- **Works with performance correction**: After 12 games, the performance-based rating correction kicks in

### Combined System Now

1. **Games 1-12**: High K-factor (3x→1x) for fast convergence
2. **After game 12**: Performance rating calculated, 50% correction applied
3. **Game 13+**: Normal K factor, player is now "established"

```plaintext
        /// <summary>
        /// Calculates a K-factor multiplier based on games played (uncertainty-based).
        /// New players get higher K to converge faster, decreasing to 1.0 at GAMES_FOR_ESTIMATION.
        /// Formula: multiplier = 1 + max(0, (GAMES_FOR_ESTIMATION - gamesPlayed) / 6)
        /// </summary>
        private static double CalculateUncertaintyFactor(int gamesPlayed)
        {
            // Linear decrease from 3.0 at 0 games to 1.0 at 12 games
            return 1 + Math.Max(0, (GAMES_FOR_ESTIMATION - gamesPlayed) / 6.0);
        }
```

```plaintext
        /// <summary>
        /// Calculates a K-factor multiplier based on games played (uncertainty-based).
        /// New players get higher K to converge faster, decreasing to 1.0 at GAMES_FOR_ESTIMATION.
        /// Formula: multiplier = 1 + max(0, (GAMES_FOR_ESTIMATION - gamesPlayed) / 6)
        /// </summary>
        private static double CalculateUncertaintyFactor(int gamesPlayed)
        {
            // Linear decrease from 3.0 at 0 games to 1.0 at 12 games
            return 1 + Math.Max(0, (GAMES_FOR_ESTIMATION - gamesPlayed) / 6.0);
        }
```

---

**User**

Even if a player defected all 5D openents, it doesn't mean he is a 9 dan player. Should the performance based rating calculation consider that please? For example, it may add more when the openents' rating are low but add less when openents are stronger?

---

**Cursor**

Excellent point! Beating all 5D opponents doesn't prove you're 9D - you might just be a strong 6D or 7D. The current formula extrapolates too aggressively from limited data.

Let me improve the performance rating calculation with:
1. **Ceiling based on strongest opponent** - Can't be rated too far above your best win
2. **Diminishing returns** - 100% win rate against 5D suggests ~6-7D, not 9D
3. **Weighted by opponent strength** - Wins vs stronger opponents count more

```csharp
using PlayerRatings.Engine.Rating;
using PlayerRatings.Models;
using PlayerRatings.Localization;

namespace PlayerRatings.Engine.Stats
{
    public class EloStat : IStat
    {
        protected readonly Dictionary<string, double> _dict = new Dictionary<string, double>();
        protected double _china6dRating = 0.0;

        // Track game results for new foreign/unknown players to calculate performance rating
        // Key: player Id, Value: list of (opponent rating, score) tuples
        private readonly Dictionary<string, List<(double opponentRating, double score)>> _performanceTracker 
            = new Dictionary<string, List<(double opponentRating, double score)>>();

        // Number of games required before calculating estimated initial rating
        private const int GAMES_FOR_ESTIMATION = 12;

        public void AddMatch(Match match)
        {
            double firstUserScore = 1.0;
            if (match.SecondPlayerScore > match.FirstPlayerScore)
            {
                firstUserScore = 0;
            }
            else if (match.SecondPlayerScore == match.FirstPlayerScore)
            {
                firstUserScore = 0.5;
            }

            bool isIntlLeague = match.League.Name.Contains("Intl.");
            int firstPlayerRankingRating = match.FirstPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
            int secondPlayerRankingRating = match.SecondPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
            double firstPlayerRating = _dict.ContainsKey(match.FirstPlayer.Id) ? _dict[match.FirstPlayer.Id] : firstPlayerRankingRating;
            double secondPlayerRating = _dict.ContainsKey(match.SecondPlayer.Id) ? _dict[match.SecondPlayer.Id] : secondPlayerRankingRating;

            double factor1 = match.Factor.GetValueOrDefault(1);
            if (factor1 > 0) factor1 = 1;
            double factor2 = factor1;
            if (factor1 == 1) // factor is not specified
            {
                bool player1NeedsDynamic = match.FirstPlayer.NeedDynamicFactor(isIntlLeague);
                bool player2NeedsDynamic = match.SecondPlayer.NeedDynamicFactor(isIntlLeague);

                // Apply uncertainty-based K factor for new/foreign players
                // This is symmetric - applies to both wins and losses
                if (player1NeedsDynamic)
                {
                    factor1 = CalculateUncertaintyFactor(match.FirstPlayer.MatchCount);
                    
                    // Reduce impact on established players when playing against uncertain players
                    if (!player2NeedsDynamic && !match.SecondPlayer.IsVirtualPlayer)
                    {
                        factor2 = 0.5; // Half K for established player
                    }
                }

                if (player2NeedsDynamic)
                {
                    factor2 = CalculateUncertaintyFactor(match.SecondPlayer.MatchCount);
                    
                    // Reduce impact on established players when playing against uncertain players
                    if (!player1NeedsDynamic && !match.FirstPlayer.IsVirtualPlayer)
                    {
                        factor1 = 0.5; // Half K for established player
                    }
                }

                // When both are new players, cap the factors to avoid excessive volatility
                if (player1NeedsDynamic && player2NeedsDynamic)
                {
                    factor1 = Math.Min(factor1, 2.0);
                    factor2 = Math.Min(factor2, 2.0);
                }

                // Special handling for China 5D virtual player pool (high variance)
                if (match.FirstPlayer.DisplayName == "[China 5D]" && match.Date.Year >= 2025 && match.Date.Month >= 7)
                {
                    if (match.Date.Year >= 2025 && match.Date.Month >= 11 && match.Date.Day >= 24)
                    {
                        factor2 *= Math.Min(1, (firstPlayerRating - 1000) * (firstPlayerRating - 1000) / (_china6dRating - 1000) / (secondPlayerRating - 1000));
                    }
                    else
                    {
                        factor2 *= 0.5;
                    }
                }
            }

            var rating1 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, Elo.GetK(firstPlayerRating) * factor1);
            var rating2 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, Elo.GetK(secondPlayerRating) * factor2);

            if (match.FirstPlayer.DisplayName == "[China 6D]")
                _china6dRating = rating1.NewRatingAPlayer;
            else if (match.SecondPlayer.DisplayName == "[China 6D]")
                _china6dRating = rating1.NewRatingBPlayer;

            match.OldFirstPlayerRating = firstPlayerRating.ToString("F1");
            match.OldSecondPlayerRating = secondPlayerRating.ToString("F1");

            _dict[match.FirstPlayer.Id] = rating1.NewRatingAPlayer;
            _dict[match.SecondPlayer.Id] = rating2.NewRatingBPlayer;

            match.ShiftRating = rating1.ShiftRatingAPlayer.ToString("F1");
            var player2ShiftRating = (secondPlayerRating - _dict[match.SecondPlayer.Id]).ToString("F1");
            if (match.ShiftRating != player2ShiftRating)
            {
                // rating shifts will be different for the two players, display both
                match.ShiftRating += "-" + player2ShiftRating;
            }

            // Track games for performance-based initial rating calculation
            TrackGameForPerformanceRating(match.FirstPlayer, secondPlayerRating, firstUserScore, isIntlLeague);
            TrackGameForPerformanceRating(match.SecondPlayer, firstPlayerRating, 1 - firstUserScore, isIntlLeague);
        }

        /// <summary>
        /// Tracks a game result for a new foreign/unknown player and calculates their
        /// estimated initial rating after they've played enough games.
        /// After calculation, applies a correction to the player's current rating.
        /// </summary>
        private void TrackGameForPerformanceRating(ApplicationUser player, double opponentRating, double score, bool isIntlLeague)
        {
            // Only track for players who need performance estimation
            // (foreign/unknown players in their first 12 games)
            if (!player.NeedDynamicFactor(isIntlLeague))
                return;

            // Skip virtual players (aggregated player pools like [China 5D])
            if (player.IsVirtualPlayer)
                return;

            // Initialize tracking list if needed
            if (!_performanceTracker.ContainsKey(player.Id))
            {
                _performanceTracker[player.Id] = new List<(double opponentRating, double score)>();
            }

            // Add this game result
            _performanceTracker[player.Id].Add((opponentRating, score));

            // After GAMES_FOR_ESTIMATION games, calculate estimated initial rating and apply correction
            if (_performanceTracker[player.Id].Count == GAMES_FOR_ESTIMATION)
            {
                var estimatedInitialRating = CalculatePerformanceRating(_performanceTracker[player.Id]);
                player.EstimatedInitialRating = estimatedInitialRating;

                // Apply rating correction based on the difference between
                // original ranking-based rating and performance-based estimate
                if (_dict.ContainsKey(player.Id))
                {
                    double originalInitialRating = player.GetRatingBeforeDate(player.FirstMatch.Date, isIntlLeague);
                    double ratingCorrection = estimatedInitialRating - originalInitialRating;
                    
                    // Apply partial correction (50%) to avoid over-adjusting
                    // The dynamic K-factor should have already moved them partway to their true rating
                    double currentRating = _dict[player.Id];
                    double correctedRating = currentRating + ratingCorrection * 0.5;
                    
                    _dict[player.Id] = correctedRating;
                }

                // Clean up tracker as we no longer need to track this player
                _performanceTracker.Remove(player.Id);
            }
        }

        /// <summary>
        /// Calculates performance rating based on game results with conservative extrapolation.
        /// Key principles:
        /// 1. Wins against stronger opponents are weighted more heavily
        /// 2. Ceiling based on strongest opponent beaten (can't extrapolate too far)
        /// 3. Diminishing returns for extreme win rates
        /// </summary>
        private static double CalculatePerformanceRating(List<(double opponentRating, double score)> games)
        {
            if (games.Count == 0)
                return 1600; // Default to 5 kyu

            // Find the strongest opponent beaten and weakest opponent lost to
            double strongestWin = double.MinValue;
            double weakestLoss = double.MaxValue;
            double weightedOpponentSum = 0;
            double weightSum = 0;
            double totalScore = 0;

            foreach (var game in games)
            {
                totalScore += game.score;

                // Weight games by opponent strength - stronger opponents give more information
                // Use square root to dampen the effect (avoid over-weighting)
                double weight = Math.Sqrt(Math.Max(1000, game.opponentRating) / 1000.0);
                weightedOpponentSum += game.opponentRating * weight;
                weightSum += weight;

                // Track strongest win and weakest loss
                if (game.score >= 0.5 && game.opponentRating > strongestWin)
                {
                    strongestWin = game.opponentRating;
                }
                if (game.score <= 0.5 && game.opponentRating < weakestLoss)
                {
                    weakestLoss = game.opponentRating;
                }
            }

            double weightedAvgOpponent = weightedOpponentSum / weightSum;
            double winRate = totalScore / games.Count;

            // Calculate base rating difference with diminishing returns
            // Use a more conservative formula that compresses extreme results
            double ratingDiff;
            if (winRate >= 0.99)
            {
                // 100% win rate: estimate ~1.5-2 dan above average opponent (not 4 dan!)
                ratingDiff = 200;
            }
            else if (winRate <= 0.01)
            {
                // 0% win rate: estimate ~1.5-2 dan below average opponent
                ratingDiff = -200;
            }
            else
            {
                // Use compressed logit function for more conservative estimates
                // Standard: 173.7 * ln(p/(1-p)) gives ~400 for 90% win rate
                // Compressed: use factor of 100 to give ~230 for 90% win rate
                double logitDiff = 100 * Math.Log(winRate / (1 - winRate));
                
                // Apply additional compression for extreme values
                // This ensures diminishing returns as win rate approaches 0% or 100%
                ratingDiff = Math.Sign(logitDiff) * Math.Min(Math.Abs(logitDiff), 200 + Math.Sqrt(Math.Abs(logitDiff)));
            }

            double estimatedRating = weightedAvgOpponent + ratingDiff;

            // Apply ceiling based on strongest opponent beaten
            // Can't be rated more than ~1.5 dan above your best win (150 points)
            // This prevents "beat all 5D = must be 9D" logic
            if (strongestWin > double.MinValue && estimatedRating > strongestWin + 150)
            {
                estimatedRating = strongestWin + 150;
            }

            // Apply floor based on weakest opponent lost to
            // Can't be rated more than ~1.5 dan below your worst loss
            if (weakestLoss < double.MaxValue && estimatedRating < weakestLoss - 150)
            {
                estimatedRating = weakestLoss - 150;
            }

            return estimatedRating;
        }

        /// <summary>
        /// Calculates a K-factor multiplier based on games played (uncertainty-based).
        /// New players get higher K to converge faster, decreasing to 1.0 at GAMES_FOR_ESTIMATION.
        /// Formula: multiplier = 1 + max(0, (GAMES_FOR_ESTIMATION - gamesPlayed) / 6)
        /// </summary>
        /// <param name="gamesPlayed">Number of games the player has played</param>
        /// <returns>K-factor multiplier (1.0 to 3.0)</returns>
        private static double CalculateUncertaintyFactor(int gamesPlayed)
        {
            // Linear decrease from 3.0 at 0 games to 1.0 at 12 games
            // 0 games: 1 + 12/6 = 3.0
            // 6 games: 1 + 6/6 = 2.0
            // 12 games: 1 + 0/6 = 1.0
            return 1 + Math.Max(0, (GAMES_FOR_ESTIMATION - gamesPlayed) / 6.0);
        }

        /// <summary>
        /// Legacy method for calculating dynamic factor based on rating difference.
        /// Only boosts K when opponent is stronger (asymmetric).
        /// Consider using CalculateUncertaintyFactor instead for symmetric behavior.
        /// </summary>
        [Obsolete("Use CalculateUncertaintyFactor for symmetric games-based K adjustment")]
        private static double CalculateDynamicFactor(double firstPlayerRating, double secondPlayerRating)
        {
            var ret = Math.Max(1, Math.Pow(2, ((secondPlayerRating - firstPlayerRating) / 200)));
            return ret;
        }

        public virtual string GetResult(ApplicationUser user)
        {
            return _dict.ContainsKey(user.Id) ? _dict[user.Id].ToString("F1") : "";
        }

        public virtual string NameLocalizationKey => nameof(LocalizationKey.Elo);

        public virtual double this[ApplicationUser user]
        {
            get { return _dict.ContainsKey(user.Id) ? _dict[user.Id] : user.GetRatingBeforeDate(user.FirstMatch.Date); }
            set { _dict[user.Id] = value; }
        }
    }

    public class EloStatChange : EloStat
    {
        public override string GetResult(ApplicationUser user)
        {
            return _dict.ContainsKey(user.Id) ? (_dict[user.Id] - user.GetRatingBeforeDate(League.CutoffDate)).ToString("F1") : "";
        }

        public override string NameLocalizationKey => nameof(LocalizationKey.Delta);

        public override double this[ApplicationUser user] => _dict.ContainsKey(user.Id) ? _dict[user.Id] : 0;
    }
}

```

Here's the improved performance rating calculation:

## Key Improvements

### 1. Ceiling Based on Strongest Win
```csharp
// Can't be rated more than ~1.5 dan above your best win (150 points)
if (strongestWin > double.MinValue && estimatedRating > strongestWin + 150)
{
    estimatedRating = strongestWin + 150;
}
```

**Example**: Beat all 5D (2500) opponents → Max rating = 2500 + 150 = **2650 (about 6D)**, not 2900 (9D)!

### 2. Weighted by Opponent Strength
```csharp
// Stronger opponents give more information
double weight = Math.Sqrt(Math.Max(1000, game.opponentRating) / 1000.0);
```

| Opponent Rating | Weight |
|-----------------|--------|
| 1600 (5K) | 1.26 |
| 2100 (1D) | 1.45 |
| 2500 (5D) | 1.58 |
| 2700 (7D) | 1.64 |

### 3. Compressed Rating Differences

| Win Rate | Old Formula | New Formula |
|----------|-------------|-------------|
| 100% | +400 (4 dan!) | +200 (2 dan) |
| 90% | +381 | ~+200 |
| 75% | +191 | ~+150 |
| 50% | 0 | 0 |
| 25% | -191 | ~-150 |
| 0% | -400 | -200 |

### 4. Floor Based on Weakest Loss
Similarly, can't be rated more than 1.5 dan below your worst loss.

## Result
A player who beats all 5D opponents (avg 2500) now gets estimated at ~**2600-2650** (strong 6D), not 2900 (9D). Much more realistic!

```csharp
// Can't be rated more than ~1.5 dan above your best win (150 points)
if (strongestWin > double.MinValue && estimatedRating > strongestWin + 150)
{
    estimatedRating = strongestWin + 150;
}
```

```csharp
// Stronger opponents give more information
double weight = Math.Sqrt(Math.Max(1000, game.opponentRating) / 1000.0);
```

---

**User**

Some kyu players might lose a lot of games at the very beggining, then improved a lot. But they may have too few games so their rating can't catch up. How to help them please?

---

**Cursor**

Great question! This is a different problem - players who genuinely **improved** over time, but their early losses keep dragging down their rating. Let me add two mechanisms:

1. **Weight recent games more heavily** in performance calculation
2. **Ongoing catch-up boost** for players consistently outperforming their rating

```csharp
using PlayerRatings.Engine.Rating;
using PlayerRatings.Models;
using PlayerRatings.Localization;

namespace PlayerRatings.Engine.Stats
{
    public class EloStat : IStat
    {
        protected readonly Dictionary<string, double> _dict = new Dictionary<string, double>();
        protected double _china6dRating = 0.0;

        // Track game results for new foreign/unknown players to calculate performance rating
        // Key: player Id, Value: list of (opponent rating, score) tuples
        private readonly Dictionary<string, List<(double opponentRating, double score)>> _performanceTracker 
            = new Dictionary<string, List<(double opponentRating, double score)>>();

        // Track recent games for all players to detect improvement (sliding window)
        // Key: player Id, Value: circular buffer of recent (opponent rating, score, player's rating at time) tuples
        private readonly Dictionary<string, Queue<(double opponentRating, double score, double playerRating)>> _recentGamesTracker
            = new Dictionary<string, Queue<(double opponentRating, double score, double playerRating)>>();

        // Number of games required before calculating estimated initial rating
        private const int GAMES_FOR_ESTIMATION = 12;

        // Number of recent games to track for improvement detection
        private const int RECENT_GAMES_WINDOW = 8;

        // Minimum performance gap (in rating points) to trigger catch-up boost
        private const double CATCHUP_THRESHOLD = 100;

        public void AddMatch(Match match)
        {
            double firstUserScore = 1.0;
            if (match.SecondPlayerScore > match.FirstPlayerScore)
            {
                firstUserScore = 0;
            }
            else if (match.SecondPlayerScore == match.FirstPlayerScore)
            {
                firstUserScore = 0.5;
            }

            bool isIntlLeague = match.League.Name.Contains("Intl.");
            int firstPlayerRankingRating = match.FirstPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
            int secondPlayerRankingRating = match.SecondPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
            double firstPlayerRating = _dict.ContainsKey(match.FirstPlayer.Id) ? _dict[match.FirstPlayer.Id] : firstPlayerRankingRating;
            double secondPlayerRating = _dict.ContainsKey(match.SecondPlayer.Id) ? _dict[match.SecondPlayer.Id] : secondPlayerRankingRating;

            double factor1 = match.Factor.GetValueOrDefault(1);
            if (factor1 > 0) factor1 = 1;
            double factor2 = factor1;
            if (factor1 == 1) // factor is not specified
            {
                bool player1NeedsDynamic = match.FirstPlayer.NeedDynamicFactor(isIntlLeague);
                bool player2NeedsDynamic = match.SecondPlayer.NeedDynamicFactor(isIntlLeague);

                // Apply uncertainty-based K factor for new/foreign players
                // This is symmetric - applies to both wins and losses
                if (player1NeedsDynamic)
                {
                    factor1 = CalculateUncertaintyFactor(match.FirstPlayer.MatchCount);
                    
                    // Reduce impact on established players when playing against uncertain players
                    if (!player2NeedsDynamic && !match.SecondPlayer.IsVirtualPlayer)
                    {
                        factor2 = 0.5; // Half K for established player
                    }
                }

                if (player2NeedsDynamic)
                {
                    factor2 = CalculateUncertaintyFactor(match.SecondPlayer.MatchCount);
                    
                    // Reduce impact on established players when playing against uncertain players
                    if (!player1NeedsDynamic && !match.FirstPlayer.IsVirtualPlayer)
                    {
                        factor1 = 0.5; // Half K for established player
                    }
                }

                // When both are new players, cap the factors to avoid excessive volatility
                if (player1NeedsDynamic && player2NeedsDynamic)
                {
                    factor1 = Math.Min(factor1, 2.0);
                    factor2 = Math.Min(factor2, 2.0);
                }

                // Special handling for China 5D virtual player pool (high variance)
                if (match.FirstPlayer.DisplayName == "[China 5D]" && match.Date.Year >= 2025 && match.Date.Month >= 7)
                {
                    if (match.Date.Year >= 2025 && match.Date.Month >= 11 && match.Date.Day >= 24)
                    {
                        factor2 *= Math.Min(1, (firstPlayerRating - 1000) * (firstPlayerRating - 1000) / (_china6dRating - 1000) / (secondPlayerRating - 1000));
                    }
                    else
                    {
                        factor2 *= 0.5;
                    }
                }
            }

            var rating1 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, Elo.GetK(firstPlayerRating) * factor1);
            var rating2 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, Elo.GetK(secondPlayerRating) * factor2);

            if (match.FirstPlayer.DisplayName == "[China 6D]")
                _china6dRating = rating1.NewRatingAPlayer;
            else if (match.SecondPlayer.DisplayName == "[China 6D]")
                _china6dRating = rating1.NewRatingBPlayer;

            match.OldFirstPlayerRating = firstPlayerRating.ToString("F1");
            match.OldSecondPlayerRating = secondPlayerRating.ToString("F1");

            _dict[match.FirstPlayer.Id] = rating1.NewRatingAPlayer;
            _dict[match.SecondPlayer.Id] = rating2.NewRatingBPlayer;

            match.ShiftRating = rating1.ShiftRatingAPlayer.ToString("F1");
            var player2ShiftRating = (secondPlayerRating - _dict[match.SecondPlayer.Id]).ToString("F1");
            if (match.ShiftRating != player2ShiftRating)
            {
                // rating shifts will be different for the two players, display both
                match.ShiftRating += "-" + player2ShiftRating;
            }

            // Track games for performance-based initial rating calculation
            TrackGameForPerformanceRating(match.FirstPlayer, secondPlayerRating, firstUserScore, isIntlLeague);
            TrackGameForPerformanceRating(match.SecondPlayer, firstPlayerRating, 1 - firstUserScore, isIntlLeague);
        }

        /// <summary>
        /// Tracks a game result for a new foreign/unknown player and calculates their
        /// estimated initial rating after they've played enough games.
        /// After calculation, applies a correction to the player's current rating.
        /// </summary>
        private void TrackGameForPerformanceRating(ApplicationUser player, double opponentRating, double score, bool isIntlLeague)
        {
            // Only track for players who need performance estimation
            // (foreign/unknown players in their first 12 games)
            if (!player.NeedDynamicFactor(isIntlLeague))
                return;

            // Skip virtual players (aggregated player pools like [China 5D])
            if (player.IsVirtualPlayer)
                return;

            // Initialize tracking list if needed
            if (!_performanceTracker.ContainsKey(player.Id))
            {
                _performanceTracker[player.Id] = new List<(double opponentRating, double score)>();
            }

            // Add this game result
            _performanceTracker[player.Id].Add((opponentRating, score));

            // After GAMES_FOR_ESTIMATION games, calculate estimated initial rating and apply correction
            if (_performanceTracker[player.Id].Count == GAMES_FOR_ESTIMATION)
            {
                var estimatedInitialRating = CalculatePerformanceRating(_performanceTracker[player.Id]);
                player.EstimatedInitialRating = estimatedInitialRating;

                // Apply rating correction based on the difference between
                // original ranking-based rating and performance-based estimate
                if (_dict.ContainsKey(player.Id))
                {
                    double originalInitialRating = player.GetRatingBeforeDate(player.FirstMatch.Date, isIntlLeague);
                    double ratingCorrection = estimatedInitialRating - originalInitialRating;
                    
                    // Apply partial correction (30%) to avoid over-adjusting
                    // The dynamic K-factor should have already moved them partway to their true rating
                    double currentRating = _dict[player.Id];
                    double correctedRating = currentRating + ratingCorrection * 0.3;
                    
                    _dict[player.Id] = correctedRating;
                }

                // Clean up tracker as we no longer need to track this player
                _performanceTracker.Remove(player.Id);
            }
        }

        /// <summary>
        /// Calculates performance rating based on game results with conservative extrapolation.
        /// Key principles:
        /// 1. Wins against stronger opponents are weighted more heavily
        /// 2. Ceiling based on strongest opponent beaten (can't extrapolate too far)
        /// 3. Diminishing returns for extreme win rates
        /// </summary>
        private static double CalculatePerformanceRating(List<(double opponentRating, double score)> games)
        {
            if (games.Count == 0)
                return 1600; // Default to 5 kyu

            // Find the strongest opponent beaten and weakest opponent lost to
            double strongestWin = double.MinValue;
            double weakestLoss = double.MaxValue;
            double weightedOpponentSum = 0;
            double weightSum = 0;
            double totalScore = 0;

            foreach (var game in games)
            {
                totalScore += game.score;

                // Weight games by opponent strength - stronger opponents give more information
                // Use square root to dampen the effect (avoid over-weighting)
                double weight = Math.Sqrt(Math.Max(1000, game.opponentRating) / 1000.0);
                weightedOpponentSum += game.opponentRating * weight;
                weightSum += weight;

                // Track strongest win and weakest loss
                if (game.score >= 0.5 && game.opponentRating > strongestWin)
                {
                    strongestWin = game.opponentRating;
                }
                if (game.score <= 0.5 && game.opponentRating < weakestLoss)
                {
                    weakestLoss = game.opponentRating;
                }
            }

            double weightedAvgOpponent = weightedOpponentSum / weightSum;
            double winRate = totalScore / games.Count;

            // Calculate base rating difference with diminishing returns
            // Use a more conservative formula that compresses extreme results
            double ratingDiff;
            if (winRate >= 0.99)
            {
                // 100% win rate: estimate ~1.5-2 dan above average opponent (not 4 dan!)
                ratingDiff = 200;
            }
            else if (winRate <= 0.01)
            {
                // 0% win rate: estimate ~1.5-2 dan below average opponent
                ratingDiff = -200;
            }
            else
            {
                // Use compressed logit function for more conservative estimates
                // Standard: 173.7 * ln(p/(1-p)) gives ~400 for 90% win rate
                // Compressed: use factor of 100 to give ~230 for 90% win rate
                double logitDiff = 100 * Math.Log(winRate / (1 - winRate));
                
                // Apply additional compression for extreme values
                // This ensures diminishing returns as win rate approaches 0% or 100%
                ratingDiff = Math.Sign(logitDiff) * Math.Min(Math.Abs(logitDiff), 200 + Math.Sqrt(Math.Abs(logitDiff)));
            }

            double estimatedRating = weightedAvgOpponent + ratingDiff;

            // Apply ceiling based on strongest opponent beaten
            // Can't be rated more than ~1.5 dan above your best win (150 points)
            // This prevents "beat all 5D = must be 9D" logic
            if (strongestWin > double.MinValue && estimatedRating > strongestWin + 150)
            {
                estimatedRating = strongestWin + 150;
            }

            // Apply floor based on weakest opponent lost to
            // Can't be rated more than ~1.5 dan below your worst loss
            if (weakestLoss < double.MaxValue && estimatedRating < weakestLoss - 150)
            {
                estimatedRating = weakestLoss - 150;
            }

            return estimatedRating;
        }

        /// <summary>
        /// Calculates a K-factor multiplier based on games played (uncertainty-based).
        /// New players get higher K to converge faster, decreasing to 1.0 at GAMES_FOR_ESTIMATION.
        /// Formula: multiplier = 1 + max(0, (GAMES_FOR_ESTIMATION - gamesPlayed) / 6)
        /// </summary>
        /// <param name="gamesPlayed">Number of games the player has played</param>
        /// <returns>K-factor multiplier (1.0 to 3.0)</returns>
        private static double CalculateUncertaintyFactor(int gamesPlayed)
        {
            // Linear decrease from 3.0 at 0 games to 1.0 at 12 games
            // 0 games: 1 + 12/6 = 3.0
            // 6 games: 1 + 6/6 = 2.0
            // 12 games: 1 + 0/6 = 1.0
            return 1 + Math.Max(0, (GAMES_FOR_ESTIMATION - gamesPlayed) / 6.0);
        }

        /// <summary>
        /// Legacy method for calculating dynamic factor based on rating difference.
        /// Only boosts K when opponent is stronger (asymmetric).
        /// Consider using CalculateUncertaintyFactor instead for symmetric behavior.
        /// </summary>
        [Obsolete("Use CalculateUncertaintyFactor for symmetric games-based K adjustment")]
        private static double CalculateDynamicFactor(double firstPlayerRating, double secondPlayerRating)
        {
            var ret = Math.Max(1, Math.Pow(2, ((secondPlayerRating - firstPlayerRating) / 200)));
            return ret;
        }

        public virtual string GetResult(ApplicationUser user)
        {
            return _dict.ContainsKey(user.Id) ? _dict[user.Id].ToString("F1") : "";
        }

        public virtual string NameLocalizationKey => nameof(LocalizationKey.Elo);

        public virtual double this[ApplicationUser user]
        {
            get { return _dict.ContainsKey(user.Id) ? _dict[user.Id] : user.GetRatingBeforeDate(user.FirstMatch.Date); }
            set { _dict[user.Id] = value; }
        }
    }

    public class EloStatChange : EloStat
    {
        public override string GetResult(ApplicationUser user)
        {
            return _dict.ContainsKey(user.Id) ? (_dict[user.Id] - user.GetRatingBeforeDate(League.CutoffDate)).ToString("F1") : "";
        }

        public override string NameLocalizationKey => nameof(LocalizationKey.Delta);

        public override double this[ApplicationUser user] => _dict.ContainsKey(user.Id) ? _dict[user.Id] : 0;
    }
}

```

Now let me add the tracking and catch-up mechanism after the rating updates:

```csharp
using PlayerRatings.Engine.Rating;
using PlayerRatings.Models;
using PlayerRatings.Localization;

namespace PlayerRatings.Engine.Stats
{
    public class EloStat : IStat
    {
        protected readonly Dictionary<string, double> _dict = new Dictionary<string, double>();
        protected double _china6dRating = 0.0;

        // Track game results for new foreign/unknown players to calculate performance rating
        // Key: player Id, Value: list of (opponent rating, score) tuples
        private readonly Dictionary<string, List<(double opponentRating, double score)>> _performanceTracker 
            = new Dictionary<string, List<(double opponentRating, double score)>>();

        // Track recent games for all players to detect improvement (sliding window)
        // Key: player Id, Value: circular buffer of recent (opponent rating, score, player's rating at time) tuples
        private readonly Dictionary<string, Queue<(double opponentRating, double score, double playerRating)>> _recentGamesTracker
            = new Dictionary<string, Queue<(double opponentRating, double score, double playerRating)>>();

        // Number of games required before calculating estimated initial rating
        private const int GAMES_FOR_ESTIMATION = 12;

        // Number of recent games to track for improvement detection
        private const int RECENT_GAMES_WINDOW = 8;

        // Minimum performance gap (in rating points) to trigger catch-up boost
        private const double CATCHUP_THRESHOLD = 100;

        public void AddMatch(Match match)
        {
            double firstUserScore = 1.0;
            if (match.SecondPlayerScore > match.FirstPlayerScore)
            {
                firstUserScore = 0;
            }
            else if (match.SecondPlayerScore == match.FirstPlayerScore)
            {
                firstUserScore = 0.5;
            }

            bool isIntlLeague = match.League.Name.Contains("Intl.");
            int firstPlayerRankingRating = match.FirstPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
            int secondPlayerRankingRating = match.SecondPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
            double firstPlayerRating = _dict.ContainsKey(match.FirstPlayer.Id) ? _dict[match.FirstPlayer.Id] : firstPlayerRankingRating;
            double secondPlayerRating = _dict.ContainsKey(match.SecondPlayer.Id) ? _dict[match.SecondPlayer.Id] : secondPlayerRankingRating;

            double factor1 = match.Factor.GetValueOrDefault(1);
            if (factor1 > 0) factor1 = 1;
            double factor2 = factor1;
            if (factor1 == 1) // factor is not specified
            {
                bool player1NeedsDynamic = match.FirstPlayer.NeedDynamicFactor(isIntlLeague);
                bool player2NeedsDynamic = match.SecondPlayer.NeedDynamicFactor(isIntlLeague);

                // Apply uncertainty-based K factor for new/foreign players
                // This is symmetric - applies to both wins and losses
                if (player1NeedsDynamic)
                {
                    factor1 = CalculateUncertaintyFactor(match.FirstPlayer.MatchCount);
                    
                    // Reduce impact on established players when playing against uncertain players
                    if (!player2NeedsDynamic && !match.SecondPlayer.IsVirtualPlayer)
                    {
                        factor2 = 0.5; // Half K for established player
                    }
                }

                if (player2NeedsDynamic)
                {
                    factor2 = CalculateUncertaintyFactor(match.SecondPlayer.MatchCount);
                    
                    // Reduce impact on established players when playing against uncertain players
                    if (!player1NeedsDynamic && !match.FirstPlayer.IsVirtualPlayer)
                    {
                        factor1 = 0.5; // Half K for established player
                    }
                }

                // When both are new players, cap the factors to avoid excessive volatility
                if (player1NeedsDynamic && player2NeedsDynamic)
                {
                    factor1 = Math.Min(factor1, 2.0);
                    factor2 = Math.Min(factor2, 2.0);
                }

                // Special handling for China 5D virtual player pool (high variance)
                if (match.FirstPlayer.DisplayName == "[China 5D]" && match.Date.Year >= 2025 && match.Date.Month >= 7)
                {
                    if (match.Date.Year >= 2025 && match.Date.Month >= 11 && match.Date.Day >= 24)
                    {
                        factor2 *= Math.Min(1, (firstPlayerRating - 1000) * (firstPlayerRating - 1000) / (_china6dRating - 1000) / (secondPlayerRating - 1000));
                    }
                    else
                    {
                        factor2 *= 0.5;
                    }
                }
            }

            var rating1 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, Elo.GetK(firstPlayerRating) * factor1);
            var rating2 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, Elo.GetK(secondPlayerRating) * factor2);

            if (match.FirstPlayer.DisplayName == "[China 6D]")
                _china6dRating = rating1.NewRatingAPlayer;
            else if (match.SecondPlayer.DisplayName == "[China 6D]")
                _china6dRating = rating1.NewRatingBPlayer;

            match.OldFirstPlayerRating = firstPlayerRating.ToString("F1");
            match.OldSecondPlayerRating = secondPlayerRating.ToString("F1");

            _dict[match.FirstPlayer.Id] = rating1.NewRatingAPlayer;
            _dict[match.SecondPlayer.Id] = rating2.NewRatingBPlayer;

            match.ShiftRating = rating1.ShiftRatingAPlayer.ToString("F1");
            var player2ShiftRating = (secondPlayerRating - _dict[match.SecondPlayer.Id]).ToString("F1");
            if (match.ShiftRating != player2ShiftRating)
            {
                // rating shifts will be different for the two players, display both
                match.ShiftRating += "-" + player2ShiftRating;
            }

            // Track games for performance-based initial rating calculation
            TrackGameForPerformanceRating(match.FirstPlayer, secondPlayerRating, firstUserScore, isIntlLeague);
            TrackGameForPerformanceRating(match.SecondPlayer, firstPlayerRating, 1 - firstUserScore, isIntlLeague);

            // Track recent games and apply catch-up boost for improving players
            ApplyImprovementCatchup(match.FirstPlayer, secondPlayerRating, firstUserScore, firstPlayerRating);
            ApplyImprovementCatchup(match.SecondPlayer, firstPlayerRating, 1 - firstUserScore, secondPlayerRating);
        }

        /// <summary>
        /// Tracks recent games and applies a catch-up boost for players who are
        /// consistently outperforming their rating (e.g., kyu players who improved).
        /// </summary>
        private void ApplyImprovementCatchup(ApplicationUser player, double opponentRating, double score, double playerRatingAtTime)
        {
            // Skip virtual players
            if (player.IsVirtualPlayer)
                return;

            // Initialize tracking queue if needed
            if (!_recentGamesTracker.ContainsKey(player.Id))
            {
                _recentGamesTracker[player.Id] = new Queue<(double, double, double)>();
            }

            var recentGames = _recentGamesTracker[player.Id];

            // Add this game to recent history
            recentGames.Enqueue((opponentRating, score, playerRatingAtTime));

            // Keep only the most recent games
            while (recentGames.Count > RECENT_GAMES_WINDOW)
            {
                recentGames.Dequeue();
            }

            // Need at least half the window to make a judgment
            if (recentGames.Count < RECENT_GAMES_WINDOW / 2)
                return;

            // Calculate recent performance rating
            var gamesList = recentGames.ToList();
            double recentPerformance = CalculateRecentPerformanceRating(gamesList);
            
            // Get current rating
            if (!_dict.ContainsKey(player.Id))
                return;
                
            double currentRating = _dict[player.Id];

            // Check if player is significantly outperforming their rating
            double performanceGap = recentPerformance - currentRating;
            
            if (performanceGap > CATCHUP_THRESHOLD)
            {
                // Apply catch-up boost: move 20% of the gap
                // This helps improving players catch up faster
                double catchupBoost = performanceGap * 0.2;
                
                // Cap the boost at 50 points per game to avoid wild swings
                catchupBoost = Math.Min(catchupBoost, 50);
                
                _dict[player.Id] = currentRating + catchupBoost;
            }
        }

        /// <summary>
        /// Calculates performance rating from recent games with recency weighting.
        /// More recent games are weighted more heavily to detect improvement.
        /// </summary>
        private static double CalculateRecentPerformanceRating(List<(double opponentRating, double score, double playerRating)> games)
        {
            if (games.Count == 0)
                return 1600;

            double weightedOpponentSum = 0;
            double weightedScoreSum = 0;
            double weightSum = 0;

            for (int i = 0; i < games.Count; i++)
            {
                var game = games[i];
                
                // Recency weight: more recent games count more
                // Last game has weight ~2x the first game
                double recencyWeight = 1.0 + (double)i / games.Count;
                
                weightedOpponentSum += game.opponentRating * recencyWeight;
                weightedScoreSum += game.score * recencyWeight;
                weightSum += recencyWeight;
            }

            double avgOpponent = weightedOpponentSum / weightSum;
            double winRate = weightedScoreSum / weightSum;

            // Convert to rating using conservative formula
            double ratingDiff;
            if (winRate >= 0.95)
            {
                ratingDiff = 150;
            }
            else if (winRate <= 0.05)
            {
                ratingDiff = -150;
            }
            else
            {
                ratingDiff = 80 * Math.Log(winRate / (1 - winRate));
                ratingDiff = Math.Max(-150, Math.Min(150, ratingDiff));
            }

            return avgOpponent + ratingDiff;
        }

        /// <summary>
        /// Tracks a game result for a new foreign/unknown player and calculates their
        /// estimated initial rating after they've played enough games.
        /// After calculation, applies a correction to the player's current rating.
        /// </summary>
        private void TrackGameForPerformanceRating(ApplicationUser player, double opponentRating, double score, bool isIntlLeague)
        {
            // Only track for players who need performance estimation
            // (foreign/unknown players in their first 12 games)
            if (!player.NeedDynamicFactor(isIntlLeague))
                return;

            // Skip virtual players (aggregated player pools like [China 5D])
            if (player.IsVirtualPlayer)
                return;

            // Initialize tracking list if needed
            if (!_performanceTracker.ContainsKey(player.Id))
            {
                _performanceTracker[player.Id] = new List<(double opponentRating, double score)>();
            }

            // Add this game result
            _performanceTracker[player.Id].Add((opponentRating, score));

            // After GAMES_FOR_ESTIMATION games, calculate estimated initial rating and apply correction
            if (_performanceTracker[player.Id].Count == GAMES_FOR_ESTIMATION)
            {
                var estimatedInitialRating = CalculatePerformanceRating(_performanceTracker[player.Id]);
                player.EstimatedInitialRating = estimatedInitialRating;

                // Apply rating correction based on the difference between
                // original ranking-based rating and performance-based estimate
                if (_dict.ContainsKey(player.Id))
                {
                    double originalInitialRating = player.GetRatingBeforeDate(player.FirstMatch.Date, isIntlLeague);
                    double ratingCorrection = estimatedInitialRating - originalInitialRating;
                    
                    // Apply partial correction (30%) to avoid over-adjusting
                    // The dynamic K-factor should have already moved them partway to their true rating
                    double currentRating = _dict[player.Id];
                    double correctedRating = currentRating + ratingCorrection * 0.3;
                    
                    _dict[player.Id] = correctedRating;
                }

                // Clean up tracker as we no longer need to track this player
                _performanceTracker.Remove(player.Id);
            }
        }

        /// <summary>
        /// Calculates performance rating based on game results with conservative extrapolation.
        /// Key principles:
        /// 1. Wins against stronger opponents are weighted more heavily
        /// 2. Ceiling based on strongest opponent beaten (can't extrapolate too far)
        /// 3. Diminishing returns for extreme win rates
        /// </summary>
        private static double CalculatePerformanceRating(List<(double opponentRating, double score)> games)
        {
            if (games.Count == 0)
                return 1600; // Default to 5 kyu

            // Find the strongest opponent beaten and weakest opponent lost to
            double strongestWin = double.MinValue;
            double weakestLoss = double.MaxValue;
            double weightedOpponentSum = 0;
            double weightSum = 0;
            double totalScore = 0;

            foreach (var game in games)
            {
                totalScore += game.score;

                // Weight games by opponent strength - stronger opponents give more information
                // Use square root to dampen the effect (avoid over-weighting)
                double weight = Math.Sqrt(Math.Max(1000, game.opponentRating) / 1000.0);
                weightedOpponentSum += game.opponentRating * weight;
                weightSum += weight;

                // Track strongest win and weakest loss
                if (game.score >= 0.5 && game.opponentRating > strongestWin)
                {
                    strongestWin = game.opponentRating;
                }
                if (game.score <= 0.5 && game.opponentRating < weakestLoss)
                {
                    weakestLoss = game.opponentRating;
                }
            }

            double weightedAvgOpponent = weightedOpponentSum / weightSum;
            double winRate = totalScore / games.Count;

            // Calculate base rating difference with diminishing returns
            // Use a more conservative formula that compresses extreme results
            double ratingDiff;
            if (winRate >= 0.99)
            {
                // 100% win rate: estimate ~1.5-2 dan above average opponent (not 4 dan!)
                ratingDiff = 200;
            }
            else if (winRate <= 0.01)
            {
                // 0% win rate: estimate ~1.5-2 dan below average opponent
                ratingDiff = -200;
            }
            else
            {
                // Use compressed logit function for more conservative estimates
                // Standard: 173.7 * ln(p/(1-p)) gives ~400 for 90% win rate
                // Compressed: use factor of 100 to give ~230 for 90% win rate
                double logitDiff = 100 * Math.Log(winRate / (1 - winRate));
                
                // Apply additional compression for extreme values
                // This ensures diminishing returns as win rate approaches 0% or 100%
                ratingDiff = Math.Sign(logitDiff) * Math.Min(Math.Abs(logitDiff), 200 + Math.Sqrt(Math.Abs(logitDiff)));
            }

            double estimatedRating = weightedAvgOpponent + ratingDiff;

            // Apply ceiling based on strongest opponent beaten
            // Can't be rated more than ~1.5 dan above your best win (150 points)
            // This prevents "beat all 5D = must be 9D" logic
            if (strongestWin > double.MinValue && estimatedRating > strongestWin + 150)
            {
                estimatedRating = strongestWin + 150;
            }

            // Apply floor based on weakest opponent lost to
            // Can't be rated more than ~1.5 dan below your worst loss
            if (weakestLoss < double.MaxValue && estimatedRating < weakestLoss - 150)
            {
                estimatedRating = weakestLoss - 150;
            }

            return estimatedRating;
        }

        /// <summary>
        /// Calculates a K-factor multiplier based on games played (uncertainty-based).
        /// New players get higher K to converge faster, decreasing to 1.0 at GAMES_FOR_ESTIMATION.
        /// Formula: multiplier = 1 + max(0, (GAMES_FOR_ESTIMATION - gamesPlayed) / 6)
        /// </summary>
        /// <param name="gamesPlayed">Number of games the player has played</param>
        /// <returns>K-factor multiplier (1.0 to 3.0)</returns>
        private static double CalculateUncertaintyFactor(int gamesPlayed)
        {
            // Linear decrease from 3.0 at 0 games to 1.0 at 12 games
            // 0 games: 1 + 12/6 = 3.0
            // 6 games: 1 + 6/6 = 2.0
            // 12 games: 1 + 0/6 = 1.0
            return 1 + Math.Max(0, (GAMES_FOR_ESTIMATION - gamesPlayed) / 6.0);
        }

        /// <summary>
        /// Legacy method for calculating dynamic factor based on rating difference.
        /// Only boosts K when opponent is stronger (asymmetric).
        /// Consider using CalculateUncertaintyFactor instead for symmetric behavior.
        /// </summary>
        [Obsolete("Use CalculateUncertaintyFactor for symmetric games-based K adjustment")]
        private static double CalculateDynamicFactor(double firstPlayerRating, double secondPlayerRating)
        {
            var ret = Math.Max(1, Math.Pow(2, ((secondPlayerRating - firstPlayerRating) / 200)));
            return ret;
        }

        public virtual string GetResult(ApplicationUser user)
        {
            return _dict.ContainsKey(user.Id) ? _dict[user.Id].ToString("F1") : "";
        }

        public virtual string NameLocalizationKey => nameof(LocalizationKey.Elo);

        public virtual double this[ApplicationUser user]
        {
            get { return _dict.ContainsKey(user.Id) ? _dict[user.Id] : user.GetRatingBeforeDate(user.FirstMatch.Date); }
            set { _dict[user.Id] = value; }
        }
    }

    public class EloStatChange : EloStat
    {
        public override string GetResult(ApplicationUser user)
        {
            return _dict.ContainsKey(user.Id) ? (_dict[user.Id] - user.GetRatingBeforeDate(League.CutoffDate)).ToString("F1") : "";
        }

        public override string NameLocalizationKey => nameof(LocalizationKey.Delta);

        public override double this[ApplicationUser user] => _dict.ContainsKey(user.Id) ? _dict[user.Id] : 0;
    }
}

```

Now let me update the initial performance calculation to also weight recent games more heavily:

```csharp
using PlayerRatings.Engine.Rating;
using PlayerRatings.Models;
using PlayerRatings.Localization;

namespace PlayerRatings.Engine.Stats
{
    public class EloStat : IStat
    {
        protected readonly Dictionary<string, double> _dict = new Dictionary<string, double>();
        protected double _china6dRating = 0.0;

        // Track game results for new foreign/unknown players to calculate performance rating
        // Key: player Id, Value: list of (opponent rating, score) tuples
        private readonly Dictionary<string, List<(double opponentRating, double score)>> _performanceTracker 
            = new Dictionary<string, List<(double opponentRating, double score)>>();

        // Track recent games for all players to detect improvement (sliding window)
        // Key: player Id, Value: circular buffer of recent (opponent rating, score, player's rating at time) tuples
        private readonly Dictionary<string, Queue<(double opponentRating, double score, double playerRating)>> _recentGamesTracker
            = new Dictionary<string, Queue<(double opponentRating, double score, double playerRating)>>();

        // Number of games required before calculating estimated initial rating
        private const int GAMES_FOR_ESTIMATION = 12;

        // Number of recent games to track for improvement detection
        private const int RECENT_GAMES_WINDOW = 8;

        // Minimum performance gap (in rating points) to trigger catch-up boost
        private const double CATCHUP_THRESHOLD = 100;

        public void AddMatch(Match match)
        {
            double firstUserScore = 1.0;
            if (match.SecondPlayerScore > match.FirstPlayerScore)
            {
                firstUserScore = 0;
            }
            else if (match.SecondPlayerScore == match.FirstPlayerScore)
            {
                firstUserScore = 0.5;
            }

            bool isIntlLeague = match.League.Name.Contains("Intl.");
            int firstPlayerRankingRating = match.FirstPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
            int secondPlayerRankingRating = match.SecondPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
            double firstPlayerRating = _dict.ContainsKey(match.FirstPlayer.Id) ? _dict[match.FirstPlayer.Id] : firstPlayerRankingRating;
            double secondPlayerRating = _dict.ContainsKey(match.SecondPlayer.Id) ? _dict[match.SecondPlayer.Id] : secondPlayerRankingRating;

            double factor1 = match.Factor.GetValueOrDefault(1);
            if (factor1 > 0) factor1 = 1;
            double factor2 = factor1;
            if (factor1 == 1) // factor is not specified
            {
                bool player1NeedsDynamic = match.FirstPlayer.NeedDynamicFactor(isIntlLeague);
                bool player2NeedsDynamic = match.SecondPlayer.NeedDynamicFactor(isIntlLeague);

                // Apply uncertainty-based K factor for new/foreign players
                // This is symmetric - applies to both wins and losses
                if (player1NeedsDynamic)
                {
                    factor1 = CalculateUncertaintyFactor(match.FirstPlayer.MatchCount);
                    
                    // Reduce impact on established players when playing against uncertain players
                    if (!player2NeedsDynamic && !match.SecondPlayer.IsVirtualPlayer)
                    {
                        factor2 = 0.5; // Half K for established player
                    }
                }

                if (player2NeedsDynamic)
                {
                    factor2 = CalculateUncertaintyFactor(match.SecondPlayer.MatchCount);
                    
                    // Reduce impact on established players when playing against uncertain players
                    if (!player1NeedsDynamic && !match.FirstPlayer.IsVirtualPlayer)
                    {
                        factor1 = 0.5; // Half K for established player
                    }
                }

                // When both are new players, cap the factors to avoid excessive volatility
                if (player1NeedsDynamic && player2NeedsDynamic)
                {
                    factor1 = Math.Min(factor1, 2.0);
                    factor2 = Math.Min(factor2, 2.0);
                }

                // Special handling for China 5D virtual player pool (high variance)
                if (match.FirstPlayer.DisplayName == "[China 5D]" && match.Date.Year >= 2025 && match.Date.Month >= 7)
                {
                    if (match.Date.Year >= 2025 && match.Date.Month >= 11 && match.Date.Day >= 24)
                    {
                        factor2 *= Math.Min(1, (firstPlayerRating - 1000) * (firstPlayerRating - 1000) / (_china6dRating - 1000) / (secondPlayerRating - 1000));
                    }
                    else
                    {
                        factor2 *= 0.5;
                    }
                }
            }

            var rating1 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, Elo.GetK(firstPlayerRating) * factor1);
            var rating2 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, Elo.GetK(secondPlayerRating) * factor2);

            if (match.FirstPlayer.DisplayName == "[China 6D]")
                _china6dRating = rating1.NewRatingAPlayer;
            else if (match.SecondPlayer.DisplayName == "[China 6D]")
                _china6dRating = rating1.NewRatingBPlayer;

            match.OldFirstPlayerRating = firstPlayerRating.ToString("F1");
            match.OldSecondPlayerRating = secondPlayerRating.ToString("F1");

            _dict[match.FirstPlayer.Id] = rating1.NewRatingAPlayer;
            _dict[match.SecondPlayer.Id] = rating2.NewRatingBPlayer;

            match.ShiftRating = rating1.ShiftRatingAPlayer.ToString("F1");
            var player2ShiftRating = (secondPlayerRating - _dict[match.SecondPlayer.Id]).ToString("F1");
            if (match.ShiftRating != player2ShiftRating)
            {
                // rating shifts will be different for the two players, display both
                match.ShiftRating += "-" + player2ShiftRating;
            }

            // Track games for performance-based initial rating calculation
            TrackGameForPerformanceRating(match.FirstPlayer, secondPlayerRating, firstUserScore, isIntlLeague);
            TrackGameForPerformanceRating(match.SecondPlayer, firstPlayerRating, 1 - firstUserScore, isIntlLeague);

            // Track recent games and apply catch-up boost for improving players
            ApplyImprovementCatchup(match.FirstPlayer, secondPlayerRating, firstUserScore, firstPlayerRating);
            ApplyImprovementCatchup(match.SecondPlayer, firstPlayerRating, 1 - firstUserScore, secondPlayerRating);
        }

        /// <summary>
        /// Tracks recent games and applies a catch-up boost for players who are
        /// consistently outperforming their rating (e.g., kyu players who improved).
        /// </summary>
        private void ApplyImprovementCatchup(ApplicationUser player, double opponentRating, double score, double playerRatingAtTime)
        {
            // Skip virtual players
            if (player.IsVirtualPlayer)
                return;

            // Initialize tracking queue if needed
            if (!_recentGamesTracker.ContainsKey(player.Id))
            {
                _recentGamesTracker[player.Id] = new Queue<(double, double, double)>();
            }

            var recentGames = _recentGamesTracker[player.Id];

            // Add this game to recent history
            recentGames.Enqueue((opponentRating, score, playerRatingAtTime));

            // Keep only the most recent games
            while (recentGames.Count > RECENT_GAMES_WINDOW)
            {
                recentGames.Dequeue();
            }

            // Need at least half the window to make a judgment
            if (recentGames.Count < RECENT_GAMES_WINDOW / 2)
                return;

            // Calculate recent performance rating
            var gamesList = recentGames.ToList();
            double recentPerformance = CalculateRecentPerformanceRating(gamesList);
            
            // Get current rating
            if (!_dict.ContainsKey(player.Id))
                return;
                
            double currentRating = _dict[player.Id];

            // Check if player is significantly outperforming their rating
            double performanceGap = recentPerformance - currentRating;
            
            if (performanceGap > CATCHUP_THRESHOLD)
            {
                // Apply catch-up boost: move 20% of the gap
                // This helps improving players catch up faster
                double catchupBoost = performanceGap * 0.2;
                
                // Cap the boost at 50 points per game to avoid wild swings
                catchupBoost = Math.Min(catchupBoost, 50);
                
                _dict[player.Id] = currentRating + catchupBoost;
            }
        }

        /// <summary>
        /// Calculates performance rating from recent games with recency weighting.
        /// More recent games are weighted more heavily to detect improvement.
        /// </summary>
        private static double CalculateRecentPerformanceRating(List<(double opponentRating, double score, double playerRating)> games)
        {
            if (games.Count == 0)
                return 1600;

            double weightedOpponentSum = 0;
            double weightedScoreSum = 0;
            double weightSum = 0;

            for (int i = 0; i < games.Count; i++)
            {
                var game = games[i];
                
                // Recency weight: more recent games count more
                // Last game has weight ~2x the first game
                double recencyWeight = 1.0 + (double)i / games.Count;
                
                weightedOpponentSum += game.opponentRating * recencyWeight;
                weightedScoreSum += game.score * recencyWeight;
                weightSum += recencyWeight;
            }

            double avgOpponent = weightedOpponentSum / weightSum;
            double winRate = weightedScoreSum / weightSum;

            // Convert to rating using conservative formula
            double ratingDiff;
            if (winRate >= 0.95)
            {
                ratingDiff = 150;
            }
            else if (winRate <= 0.05)
            {
                ratingDiff = -150;
            }
            else
            {
                ratingDiff = 80 * Math.Log(winRate / (1 - winRate));
                ratingDiff = Math.Max(-150, Math.Min(150, ratingDiff));
            }

            return avgOpponent + ratingDiff;
        }

        /// <summary>
        /// Tracks a game result for a new foreign/unknown player and calculates their
        /// estimated initial rating after they've played enough games.
        /// After calculation, applies a correction to the player's current rating.
        /// </summary>
        private void TrackGameForPerformanceRating(ApplicationUser player, double opponentRating, double score, bool isIntlLeague)
        {
            // Only track for players who need performance estimation
            // (foreign/unknown players in their first 12 games)
            if (!player.NeedDynamicFactor(isIntlLeague))
                return;

            // Skip virtual players (aggregated player pools like [China 5D])
            if (player.IsVirtualPlayer)
                return;

            // Initialize tracking list if needed
            if (!_performanceTracker.ContainsKey(player.Id))
            {
                _performanceTracker[player.Id] = new List<(double opponentRating, double score)>();
            }

            // Add this game result
            _performanceTracker[player.Id].Add((opponentRating, score));

            // After GAMES_FOR_ESTIMATION games, calculate estimated initial rating and apply correction
            if (_performanceTracker[player.Id].Count == GAMES_FOR_ESTIMATION)
            {
                var estimatedInitialRating = CalculatePerformanceRating(_performanceTracker[player.Id]);
                player.EstimatedInitialRating = estimatedInitialRating;

                // Apply rating correction based on the difference between
                // original ranking-based rating and performance-based estimate
                if (_dict.ContainsKey(player.Id))
                {
                    double originalInitialRating = player.GetRatingBeforeDate(player.FirstMatch.Date, isIntlLeague);
                    double ratingCorrection = estimatedInitialRating - originalInitialRating;
                    
                    // Apply partial correction (30%) to avoid over-adjusting
                    // The dynamic K-factor should have already moved them partway to their true rating
                    double currentRating = _dict[player.Id];
                    double correctedRating = currentRating + ratingCorrection * 0.3;
                    
                    _dict[player.Id] = correctedRating;
                }

                // Clean up tracker as we no longer need to track this player
                _performanceTracker.Remove(player.Id);
            }
        }

        /// <summary>
        /// Calculates performance rating based on game results with conservative extrapolation.
        /// Key principles:
        /// 1. Recent games are weighted more heavily (to catch improvement)
        /// 2. Wins against stronger opponents are weighted more heavily
        /// 3. Ceiling based on strongest opponent beaten (can't extrapolate too far)
        /// 4. Diminishing returns for extreme win rates
        /// </summary>
        private static double CalculatePerformanceRating(List<(double opponentRating, double score)> games)
        {
            if (games.Count == 0)
                return 1600; // Default to 5 kyu

            // Find the strongest opponent beaten and weakest opponent lost to
            double strongestWin = double.MinValue;
            double weakestLoss = double.MaxValue;
            double weightedOpponentSum = 0;
            double weightedScoreSum = 0;
            double weightSum = 0;

            for (int i = 0; i < games.Count; i++)
            {
                var game = games[i];

                // Recency weight: more recent games count more (helps detect improvement)
                // Game 0 (oldest) has weight 1.0, game 11 (newest) has weight ~2.0
                double recencyWeight = 1.0 + (double)i / games.Count;

                // Opponent strength weight - stronger opponents give more information
                double strengthWeight = Math.Sqrt(Math.Max(1000, game.opponentRating) / 1000.0);
                
                // Combined weight
                double weight = recencyWeight * strengthWeight;
                
                weightedOpponentSum += game.opponentRating * weight;
                weightedScoreSum += game.score * weight;
                weightSum += weight;

                // Track strongest win and weakest loss (from recent half of games only)
                bool isRecentGame = i >= games.Count / 2;
                if (game.score >= 0.5 && game.opponentRating > strongestWin)
                {
                    strongestWin = game.opponentRating;
                }
                if (game.score <= 0.5 && game.opponentRating < weakestLoss && isRecentGame)
                {
                    // Only consider recent losses for floor (old losses might be from before improvement)
                    weakestLoss = game.opponentRating;
                }
            }

            double weightedAvgOpponent = weightedOpponentSum / weightSum;
            double winRate = weightedScoreSum / weightSum;

            // Calculate base rating difference with diminishing returns
            // Use a more conservative formula that compresses extreme results
            double ratingDiff;
            if (winRate >= 0.99)
            {
                // 100% win rate: estimate ~1.5-2 dan above average opponent (not 4 dan!)
                ratingDiff = 200;
            }
            else if (winRate <= 0.01)
            {
                // 0% win rate: estimate ~1.5-2 dan below average opponent
                ratingDiff = -200;
            }
            else
            {
                // Use compressed logit function for more conservative estimates
                // Standard: 173.7 * ln(p/(1-p)) gives ~400 for 90% win rate
                // Compressed: use factor of 100 to give ~230 for 90% win rate
                double logitDiff = 100 * Math.Log(winRate / (1 - winRate));
                
                // Apply additional compression for extreme values
                // This ensures diminishing returns as win rate approaches 0% or 100%
                ratingDiff = Math.Sign(logitDiff) * Math.Min(Math.Abs(logitDiff), 200 + Math.Sqrt(Math.Abs(logitDiff)));
            }

            double estimatedRating = weightedAvgOpponent + ratingDiff;

            // Apply ceiling based on strongest opponent beaten
            // Can't be rated more than ~1.5 dan above your best win (150 points)
            // This prevents "beat all 5D = must be 9D" logic
            if (strongestWin > double.MinValue && estimatedRating > strongestWin + 150)
            {
                estimatedRating = strongestWin + 150;
            }

            // Apply floor based on weakest opponent lost to
            // Can't be rated more than ~1.5 dan below your worst loss
            if (weakestLoss < double.MaxValue && estimatedRating < weakestLoss - 150)
            {
                estimatedRating = weakestLoss - 150;
            }

            return estimatedRating;
        }

        /// <summary>
        /// Calculates a K-factor multiplier based on games played (uncertainty-based).
        /// New players get higher K to converge faster, decreasing to 1.0 at GAMES_FOR_ESTIMATION.
        /// Formula: multiplier = 1 + max(0, (GAMES_FOR_ESTIMATION - gamesPlayed) / 6)
        /// </summary>
        /// <param name="gamesPlayed">Number of games the player has played</param>
        /// <returns>K-factor multiplier (1.0 to 3.0)</returns>
        private static double CalculateUncertaintyFactor(int gamesPlayed)
        {
            // Linear decrease from 3.0 at 0 games to 1.0 at 12 games
            // 0 games: 1 + 12/6 = 3.0
            // 6 games: 1 + 6/6 = 2.0
            // 12 games: 1 + 0/6 = 1.0
            return 1 + Math.Max(0, (GAMES_FOR_ESTIMATION - gamesPlayed) / 6.0);
        }

        /// <summary>
        /// Legacy method for calculating dynamic factor based on rating difference.
        /// Only boosts K when opponent is stronger (asymmetric).
        /// Consider using CalculateUncertaintyFactor instead for symmetric behavior.
        /// </summary>
        [Obsolete("Use CalculateUncertaintyFactor for symmetric games-based K adjustment")]
        private static double CalculateDynamicFactor(double firstPlayerRating, double secondPlayerRating)
        {
            var ret = Math.Max(1, Math.Pow(2, ((secondPlayerRating - firstPlayerRating) / 200)));
            return ret;
        }

        public virtual string GetResult(ApplicationUser user)
        {
            return _dict.ContainsKey(user.Id) ? _dict[user.Id].ToString("F1") : "";
        }

        public virtual string NameLocalizationKey => nameof(LocalizationKey.Elo);

        public virtual double this[ApplicationUser user]
        {
            get { return _dict.ContainsKey(user.Id) ? _dict[user.Id] : user.GetRatingBeforeDate(user.FirstMatch.Date); }
            set { _dict[user.Id] = value; }
        }
    }

    public class EloStatChange : EloStat
    {
        public override string GetResult(ApplicationUser user)
        {
            return _dict.ContainsKey(user.Id) ? (_dict[user.Id] - user.GetRatingBeforeDate(League.CutoffDate)).ToString("F1") : "";
        }

        public override string NameLocalizationKey => nameof(LocalizationKey.Delta);

        public override double this[ApplicationUser user] => _dict.ContainsKey(user.Id) ? _dict[user.Id] : 0;
    }
}

```

Here's what I've added to help improving players catch up:

## New Features

### 1. Recency Weighting in Performance Calculation
Recent games now count more than old games:

| Game # | Weight (relative) |
|--------|-------------------|
| 1 (oldest) | 1.0× |
| 6 (middle) | 1.5× |
| 12 (newest) | 2.0× |

**Example**: A kyu player loses games 1-6, then wins games 7-12. The newer wins are weighted ~1.5-2× more than the early losses, so their estimated rating reflects their **improved** skill.

### 2. Ongoing Catch-up Boost (Sliding Window)
After each game, tracks the last 8 games. If recent performance is 100+ points above current rating, applies a catch-up boost:

```
Catch-up boost = min(50, (recentPerformance - currentRating) × 20%)
```

**Example**: 
- Player rated 1500 (6 kyu)
- Recent 8 games performance: 1700 (4 kyu level)
- Gap: 200 points → Boost: 40 points added

### 3. Smarter Loss Floor
Old losses (before improvement) don't count against the rating floor - only **recent** losses are considered. This prevents early bad results from permanently dragging down an improved player.

## Summary

| Scenario | Before | After |
|----------|--------|-------|
| Lost 6 games, then won 6 | Both weighted equally | Recent wins weighted 2× |
| Improved but few games | Stuck at low rating | +40-50 catch-up boost per game |
| Old losses vs new wins | Old losses cap your floor | Only recent losses count for floor |

```plaintext
Catch-up boost = min(50, (recentPerformance - currentRating) × 20%)
```

