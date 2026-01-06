using System;

namespace PlayerRatings.Engine.Rating
{
    public class Elo
    {
        private const double Denominator = 400;
        
        internal readonly double OldRatingPlayerA;
        internal readonly double OldRatingPlayerB;

        public static bool SwaRankedPlayersOnly = false;

        /// <summary>
        /// Calculates the expected score (Se)
        /// </summary>
        private static double ExpectedScore(double playerRating, double opponentRating)
        {
            return 1 / (1 + Math.Pow(10, (opponentRating - playerRating) / Denominator));
        }

        /// <summary>
        /// Calculates the K factor (volatility) for a player based on rating tiers.
        /// Pro (2720+): K = 6
        /// 3D+ (2300-2719): K = 12
        /// 5K-2D (1950-2299): K = 20
        /// 15K-6K (1600-1949): K = 28
        /// 16K and below (&lt;1600): K = 36
        /// </summary>
        public static double GetK(double rating)
        {
            // EGF formulaic approach:
            // return Math.Pow((3300 - rating) / 200, 1.6);
            //
            // EGF K values by rank (for reference):
            // 5K (1950): 21.2    1K (2050): 18.8    4D (2400): 11.1
            // 4K (1975): 20.6    1D (2100): 17.6    5D (2500):  9.2
            // 3K (2000): 20.0    2D (2200): 15.3    6D (2600):  7.4
            // 2K (2025): 19.4    3D (2300): 13.1    7D (2700):  5.8
            //                                       1P (2740):  5.2
            
            if (rating >= 2720) return 6;  // EGF: 5.5 at 2720
            if (rating >= 2300) return 12; // EGF: 13.1 at 2300
            if (rating >= 1950) return 20; // EGF: 21.2 at 1950
            if (rating >= 1600) return 28; // EGF: 30.7 at 1600
            return 36;
        }

        /// <summary>
        /// Creates an Elo rating calculation using the rating system.
        /// Rating update formula: r' = r + con * (Sa - Se) + bonus
        /// </summary>
        /// <param name="playerARating">rating of player A (r1)</param>
        /// <param name="playerBRating">rating of player B (r2)</param>
        /// <param name="playerAScore">Actual game result for player A (1.0 = win, 0.5 = jigo, 0.0 = loss)</param>
        /// <param name="playerBScore">Actual game result for player B (1.0 = win, 0.5 = jigo, 0.0 = loss)</param>
        /// <param name="conFactor">The con factor (volatility) to use, typically from GetK() possibly multiplied by an adjustment factor</param>
        public Elo(double playerARating, double playerBRating, double playerAScore, double playerBScore, double conFactor)
        {
            OldRatingPlayerA = playerARating;
            OldRatingPlayerB = playerBRating;

            // Calculate expected scores using Bradley-Terry formula
            var expectedScoreA = ExpectedScore(playerARating, playerBRating);
            var expectedScoreB = ExpectedScore(playerBRating, playerARating);

            // Update ratings: r' = r + con * (Sa - Se) + bonus
            // Minimum rating is 900 per rules
            NewRatingAPlayer = Math.Max(playerARating + conFactor * (playerAScore - expectedScoreA), 900);
            NewRatingBPlayer = Math.Max(playerBRating + conFactor * (playerBScore - expectedScoreB), 900);
        }

        public Elo(double playerARating, double playerBRating, double playerAScore, double playerBScore)
            : this(playerARating, playerBRating, playerAScore, playerBScore, GetK(playerARating))
        { }

        public double NewRatingAPlayer { get; }

        public double NewRatingBPlayer { get; }

        public double ShiftRatingAPlayer => NewRatingAPlayer - OldRatingPlayerA;

        public double ShiftRatingBPlayer => NewRatingBPlayer - OldRatingPlayerB;
    }
}
