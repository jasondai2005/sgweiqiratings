using System;

namespace PlayerRatings.Engine.Rating
{
    public class Elo
    {
        internal readonly double OldRatingPlayerA;
        internal readonly double OldRatingPlayerB;

        public static bool SwaRankedPlayersOnly = false;

        /// <summary>
        /// Calculates the β (beta) mapping function for ratings.
        /// β(r) = -7 * ln(3300 - r)
        /// </summary>
        private static double Beta(double rating)
        {
            return -7 * Math.Log(3300 - rating);
        }

        /// <summary>
        /// Calculates the expected score (Se) using the Bradley-Terry formula.
        /// Se = 1 / (1 + exp(β(r2) - β(r1)))
        /// where r1 is the player's rating and r2 is the opponent's rating.
        /// </summary>
        private static double ExpectedScore(double playerRating, double opponentRating)
        {
            return 1 / (1 + Math.Exp(Beta(opponentRating) - Beta(playerRating)));
        }

        /// <summary>
        /// Calculates the con factor (volatility) for a player, similar to K in regular Elo.
        /// con = ((3300 - r) / 200)^1.6
        /// </summary>
        public static double GetK(double rating)
        {
            return Math.Pow((3300 - rating) / 200, 2);
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
            // Minimum rating is -900 per rules
            NewRatingAPlayer = Math.Max(playerARating + conFactor * (playerAScore - expectedScoreA), -900);
            NewRatingBPlayer = Math.Max(playerBRating + conFactor * (playerBScore - expectedScoreB), -900);
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
