using PlayerRatings.Engine.Rating;
using PlayerRatings.Models;
using PlayerRatings.Localization;

namespace PlayerRatings.Engine.Stats
{
    public class EloStat : IStat
    {
        protected readonly Dictionary<string, double> _dict = new Dictionary<string, double>();

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
                        factor2 = match.SecondPlayer.IsVirtualPlayer ? 1 : 0.5; // half of the normal K
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
                    factor1 = match.FirstPlayer.IsVirtualPlayer ? 1 : 0.5;
                }

                if (match.FirstPlayer.DisplayName == "[China 5D]" && match.Date.Year >= 2025 && match.Date.Month >= 7)
                {
                    // china 5d players' strenghs varies too much.
                    // reduce their impacts from Jul 2025
                    factor2 *= 0.5;
                }
            }

            var rating1 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, (firstPlayerRating >= 2200 ? Elo.LowerK : Elo.K) * factor1);
            var rating2 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, (secondPlayerRating >= 2200 ? Elo.LowerK : Elo.K) * factor2);

            match.OldFirstPlayerRating = rating1.OldRatingPlayerA.ToString("F1");
            match.OldSecondPlayerRating = rating1.OldRatingPlayerB.ToString("F1");

            // protected ratings only apply to players with a local ranking
            _dict[match.FirstPlayer.Id] = Elo.SupportProtectedRatings && !match.FirstPlayer.IsVirtualPlayer ?
                Math.Max(rating1.NewRatingAPlayer, match.FirstPlayer.GetRatingBeforeDate(match.Date.Date, true)) :
                rating1.NewRatingAPlayer;
            _dict[match.SecondPlayer.Id] = Elo.SupportProtectedRatings && !match.SecondPlayer.IsVirtualPlayer ?
                Math.Max(rating2.NewRatingBPlayer, match.SecondPlayer.GetRatingBeforeDate(match.Date.Date, true)) :
                rating2.NewRatingBPlayer;

            match.ShiftRating = rating1.ShiftRatingAPlayer.ToString("F1");
            var player2ShiftRating = secondPlayerRating - _dict[match.SecondPlayer.Id];
            if (Math.Abs(player2ShiftRating - rating1.ShiftRatingAPlayer) >= 0.01)
            {
                // rating shifts will be different for the two players, display both
                match.ShiftRating += "-" + player2ShiftRating.ToString("F1");
            }
        }

        private static double CalculateDynamicFactor(double firstPlayerRating, double secondPlayerRating)
        {
            // 40 is the ranking diff between two sequencial dans
            return Math.Max(1, Math.Pow(2, ((secondPlayerRating - firstPlayerRating) / 40)));
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
            return _dict.ContainsKey(user.Id) ? (_dict[user.Id] - user.GetRatingBeforeDate(user.FirstMatch.Date)).ToString("F1") : "";
        }

        public override string NameLocalizationKey => nameof(LocalizationKey.Delta);

        public override double this[ApplicationUser user] => _dict.ContainsKey(user.Id) ? _dict[user.Id] : 0;
    }
}
