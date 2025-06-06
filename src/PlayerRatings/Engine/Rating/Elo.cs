using System;

namespace PlayerRatings.Engine.Rating
{
    public class Elo
    {
        private const double Denominator = 400;

        public const int K = 16;
        public const int LowerK = 10;

        internal readonly double OldRatingPlayerA;
        internal readonly double OldRatingPlayerB;

        public static bool SupportProtectedRatings = false;

        public Elo(double playerARating, double playerBRating, double playerAScore, double playerBScore, double k)
        {
            OldRatingPlayerA = playerARating;
            OldRatingPlayerB = playerBRating;

            var expectedScoreA = 1 / (1 + Math.Pow(10, (playerBRating - playerARating) / Denominator));
            var expectedScoreB = 1 / (1 + Math.Pow(10, (playerARating - playerBRating) / Denominator));

            NewRatingAPlayer = playerARating + k * (playerAScore - expectedScoreA);
            NewRatingBPlayer = playerBRating + k * (playerBScore - expectedScoreB);
        }

        public Elo(double playerARating, double playerBRating, double playerAScore, double playerBScore)
            : this(playerARating, playerBRating, playerAScore, playerBScore, K)
        { }

        public double NewRatingAPlayer { get; }

        public double NewRatingBPlayer { get; }

        public double ShiftRatingAPlayer => NewRatingAPlayer - OldRatingPlayerA;

        public double ShiftRatingBPlayer => NewRatingBPlayer - OldRatingPlayerB;
    }
}
