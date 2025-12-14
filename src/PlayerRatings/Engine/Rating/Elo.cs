using System;

namespace PlayerRatings.Engine.Rating
{
    public class Elo
    {
        private const double Denominator = 400;

        public const int K = 16;
        public const int ProK = 8;
        public const int HighKyuK = 20;
        public const int MiddleKyuK = 24;

        public static int GetK(double rating)
        {
            if (rating >= 2740)
                return ProK;
            if (rating >= 2400)
                return K;
            if (rating >= 1950)
                return HighKyuK;
            return MiddleKyuK;
        }

        internal readonly double OldRatingPlayerA;
        internal readonly double OldRatingPlayerB;

        public static bool SwaRankedPlayersOnly = false;

        public Elo(double playerARating, double playerBRating, double playerAScore, double playerBScore, double k)
        {
            OldRatingPlayerA = playerARating;
            OldRatingPlayerB = playerBRating;

            var expectedScoreA = 1 / (1 + Math.Pow(10, (playerBRating - playerARating) / Denominator));
            var expectedScoreB = 1 / (1 + Math.Pow(10, (playerARating - playerBRating) / Denominator));

            NewRatingAPlayer = Math.Max(playerARating + k * (playerAScore - expectedScoreA), 600);
            NewRatingBPlayer = Math.Max(playerBRating + k * (playerBScore - expectedScoreB), 600);
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
