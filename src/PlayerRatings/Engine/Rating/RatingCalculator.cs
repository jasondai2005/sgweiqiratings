using System;
using System.Text.RegularExpressions;

namespace PlayerRatings.Engine.Rating
{
    /// <summary>
    /// Centralized rating calculation utility.
    /// All rating-related constants and calculations should use this class.
    /// </summary>
    public static class RatingCalculator
    {
        // Rating Scale Constants
        public const int ONE_D_RATING = 2100;
        public const int ONE_P_RATING = 2740;  // 1P > 7D (2700)
        public const int DAN_GRADE_DIFF = 100;
        public const int PRO_GRADE_DIFF = 40;
        public const int DEFAULT_RATING = 1700;
        public const int MIN_RATING = -900;

        // Rating thresholds for determining single-rank differences
        public const int THRESHOLD_2D_PLUS = 2200;
        public const int THRESHOLD_1D = 2100;
        public const int THRESHOLD_1K_4K = 1950;  // > this = 1K-4K range
        public const int THRESHOLD_5K_9K = 1800;  // > this = 5K-9K range
        public const int THRESHOLD_10K_19K = 1400; // > this = 10K-19K range

        /// <summary>
        /// Gets rating for professional grade.
        /// 1P = 2740, 2P = 2780, ..., 9P = 3060
        /// </summary>
        public static int GetProRating(int pro)
        {
            return ONE_P_RATING + (pro - 1) * PRO_GRADE_DIFF;
        }

        /// <summary>
        /// Gets rating for dan grade.
        /// 1D = 2100, 2D = 2200, ..., 6D = 2600, 7D = 2700
        /// </summary>
        public static int GetDanRating(int dan)
        {
            return ONE_D_RATING + (dan - 1) * DAN_GRADE_DIFF;
        }

        /// <summary>
        /// Gets rating for kyu grade.
        /// 1K=2050, 5K=1950, 10K=1800, 20K=1400, 30K=900
        /// </summary>
        public static int GetKyuRating(int kyu)
        {
            // Variable kyu differences:
            // 1K: 50 points below 1D (1K = 2050)
            // 2K-5K: 25 points per level (5K = 1950)
            // 6K-10K: 30 points per level (10K = 1800)
            // 11K-20K: 40 points per level (20K = 1400)
            // 21K-30K: 50 points per level (30K = 900)
            
            int rating = ONE_D_RATING;
            for (int k = 1; k <= kyu; k++)
            {
                rating -= GetKyuDifference(k);
            }
            return Math.Max(rating, MIN_RATING);
        }

        /// <summary>
        /// Gets the point difference for a specific kyu level.
        /// </summary>
        public static int GetKyuDifference(int kyu)
        {
            if (kyu == 1)
                return 50;  // 1K = 2050
            else if (kyu <= 5)
                return 25;  // 2K-5K
            else if (kyu <= 10)
                return 30;  // 6K-10K
            else if (kyu <= 20)
                return 40;  // 11K-20K
            else
                return 50;  // 21K-30K
        }

        /// <summary>
        /// Gets the single-rank difference based on a rating value.
        /// Used for promotion bonus calculations.
        /// </summary>
        public static int GetSingleRankDifference(int rating)
        {
            if (rating >= THRESHOLD_2D_PLUS)
                return DAN_GRADE_DIFF;  // 2D+ = 100
            else if (rating >= THRESHOLD_1D)
                return 50;  // 1D = 50 (diff to 1K)
            else if (rating > THRESHOLD_1K_4K)
                return 25;  // 1K-4K
            else if (rating > THRESHOLD_5K_9K)
                return 30;  // 5K-9K
            else if (rating > THRESHOLD_10K_19K)
                return 40;  // 10K-19K
            else
                return 50;  // 20K+
        }

        /// <summary>
        /// Calculates rating based on ranking grade and organization.
        /// </summary>
        /// <param name="rankingGrade">The ranking grade (e.g., "1D", "5K", "9P")</param>
        /// <param name="organization">Organization (SWA, TGA, or foreign)</param>
        /// <param name="intl">Whether this is for international league (no adjustments)</param>
        public static int CalculateRating(string rankingGrade, string organization, bool intl = false)
        {
            if (string.IsNullOrEmpty(rankingGrade))
                return DEFAULT_RATING;

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
                    // except (1D) which is 2075 (between SWA 1K=2050 and 1D=2100)
                    if (rankingNum == 1)
                        delta = -25;  // (1D) = 2075
                    else
                        rankingNum -= 1;  // (2D) = 2100, (3D) = 2200, etc.
                }
            }

            if (isPro)
            {
                return GetProRating(rankingNum);
            }
            else if (isDan)
            {
                return GetDanRating(rankingNum) + delta;
            }
            else if (isKyu)
            {
                return GetKyuRating(rankingNum);
            }
            else
            {
                return DEFAULT_RATING;
            }
        }

        /// <summary>
        /// Extracts effective ranking from a combined ranking string.
        /// E.g., "1D (2D)" -> "1D" or "(2D)"
        /// </summary>
        public static string GetEffectiveRanking(string ranking)
        {
            if (string.IsNullOrEmpty(ranking))
                return ranking;

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
        /// Parses a legacy ranking string and extracts organization and grade.
        /// </summary>
        public static (string grade, string organization) ParseRankingString(string ranking)
        {
            if (string.IsNullOrEmpty(ranking))
                return (string.Empty, "SWA");

            ranking = GetEffectiveRanking(ranking);

            if (ranking.Contains('['))
            {
                // Foreign: [1D CWA] -> extract organization
                var match = Regex.Match(ranking, @"\[([^\s]+)\s*([^\]]*)\]");
                string grade = match.Success ? match.Groups[1].Value : ranking.Replace("[", "").Replace("]", "");
                return (grade, "Foreign");
            }
            else if (ranking.Contains('('))
            {
                // TGA: (1D)
                return (ranking.Replace("(", "").Replace(")", ""), "TGA");
            }
            else
            {
                // SWA: 1D
                return (ranking, "SWA");
            }
        }
    }
}

