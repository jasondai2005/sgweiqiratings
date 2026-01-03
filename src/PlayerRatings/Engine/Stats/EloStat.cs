using PlayerRatings.Engine.Rating;
using PlayerRatings.Models;
using PlayerRatings.Localization;

namespace PlayerRatings.Engine.Stats
{
    public class EloStat : IStat
    {
        protected readonly Dictionary<string, double> _dict = new Dictionary<string, double>();

        // Track game results for new foreign/unknown players to calculate performance rating
        // Key: player Id, Value: list of (opponent rating, score) tuples
        private readonly Dictionary<string, List<(double opponentRating, double score)>> _performanceTracker 
            = new Dictionary<string, List<(double opponentRating, double score)>>();

        // Number of games required before calculating estimated initial rating
        private const int GAMES_FOR_ESTIMATION = 12;

        // Debug switch: set to false to disable promotion bonus for testing
        public static bool PromotionBonusEnabled { get; set; } = true;

        // Static setting to control SWA Only mode (shows TGA as foreign rankings)
        // TGA promotions still trigger promotion bonus even in SWA Only mode
        public static bool SwaOnly { get; set; } = false;

        public void AddMatch(Match match)
        {
            // Skip bye matches (either player is NULL)
            if (match.FirstPlayer == null || match.SecondPlayer == null)
                return;
                
            double firstUserScore = 1.0;
            if (match.SecondPlayerScore > match.FirstPlayerScore)
            {
                firstUserScore = 0;
            }
            else if (match.SecondPlayerScore == match.FirstPlayerScore)
            {
                firstUserScore = 0.5;
            }

            bool isSgLeague = match.League?.Name?.Contains("Singapore Weiqi") ?? false;
            
            // Check for promotions BEFORE calculating match Elo
            // This ensures promotion bonus is applied before the match result is calculated
            CheckPlayerPromotion(match.FirstPlayer, match.Date, isSgLeague);
            CheckPlayerPromotion(match.SecondPlayer, match.Date, isSgLeague);
            
            int firstPlayerRankingRating = match.FirstPlayer.GetRatingBeforeDate(match.Date.Date, !isSgLeague);
            int secondPlayerRankingRating = match.SecondPlayer.GetRatingBeforeDate(match.Date.Date, !isSgLeague);
            double firstPlayerRating = _dict.TryGetValue(match.FirstPlayer.Id, out var cachedFirst) ? cachedFirst : firstPlayerRankingRating;
            double secondPlayerRating = _dict.TryGetValue(match.SecondPlayer.Id, out var cachedSecond) ? cachedSecond : secondPlayerRankingRating;

            double factor1 = match.Factor.GetValueOrDefault(1);
            double factor2 = factor1;
            if (factor1 == 1) // factor is not specified
            {
                bool player1NeedsDynamic = match.FirstPlayer.NeedDynamicFactor(!isSgLeague);
                bool player2NeedsDynamic = match.SecondPlayer.NeedDynamicFactor(!isSgLeague);

                // Apply uncertainty-based K factor for new/foreign players
                // This is symmetric - applies to both wins and losses
                if (player1NeedsDynamic)
                {
                    factor1 = CalculateUncertaintyFactor(match.FirstPlayer.MatchCount);
                    
                    // Reduce impact on established players when playing against uncertain players
                    // Exception: Pro players always use their own K factor
                    if (!player2NeedsDynamic && !match.SecondPlayer.IsProPlayer)
                    {
                        factor2 = 0.5; // Half K for established player
                    }
                }

                if (player2NeedsDynamic)
                {
                    factor2 = CalculateUncertaintyFactor(match.SecondPlayer.MatchCount);
                    
                    // Reduce impact on established players when playing against uncertain players
                    if (!player1NeedsDynamic && !match.FirstPlayer.IsProPlayer)
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
            }

            var rating = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, Elo.GetK(firstPlayerRating) * factor1);
            Elo specialRating = factor1 != factor2 || match.SecondPlayer.IsProPlayer ? 
                specialRating = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, Elo.GetK(secondPlayerRating) * factor2) :
                rating;

            match.OldFirstPlayerRating = firstPlayerRating.ToString("F1");
            match.OldSecondPlayerRating = secondPlayerRating.ToString("F1");

            _dict[match.FirstPlayer.Id] = rating.NewRatingAPlayer;
            _dict[match.SecondPlayer.Id] = specialRating.NewRatingBPlayer;

            match.ShiftRating = rating.ShiftRatingAPlayer.ToString("F1");
            var player2ShiftRating = (secondPlayerRating - _dict[match.SecondPlayer.Id]).ToString("F1");
            if (match.ShiftRating != player2ShiftRating)
            {
                // rating shifts will be different for the two players, display both
                match.ShiftRating += "-" + player2ShiftRating;
            }

            // Track games for performance-based initial rating calculation
            TrackGameForPerformanceRating(match.FirstPlayer, secondPlayerRating, firstUserScore, isSgLeague);
            TrackGameForPerformanceRating(match.SecondPlayer, firstPlayerRating, 1 - firstUserScore, isSgLeague);
        }

        // Track the last known ranking for each player to detect promotions
        private readonly Dictionary<string, PlayerRanking> _lastKnownRanking = new Dictionary<string, PlayerRanking>();

        // Track promotion bonuses applied to players: Key = playerId, Value = list of (date, fromRanking, fromOrg, toRanking, toOrg, bonusAmount)
        private readonly Dictionary<string, List<(DateTimeOffset date, string fromRanking, string fromOrg, string toRanking, string toOrg, double bonusAmount)>> 
            _promotionBonuses = new Dictionary<string, List<(DateTimeOffset, string, string, string, string, double)>>();

        /// <summary>
        /// Checks and applies promotion bonus for a player at a specific date.
        /// Since RankingDate is treated as end-of-day (23:59:59), promotions are only
        /// visible after their effective time, so we can apply the bonus immediately.
        /// </summary>
        public void CheckPlayerPromotion(ApplicationUser player, DateTimeOffset date, bool isSgLeague)
        {
            // Get current ranking at date (rankings only visible after their end-of-day effective time)
            player.GetCombinedRankingBeforeDate(date, out PlayerRanking currentRanking);
            if (currentRanking == null || string.IsNullOrEmpty(currentRanking.Ranking))
                return;

            // Skip if promotion bonus is disabled
            if (!PromotionBonusEnabled)
                return;

            // Check if ranking changed (promotion detected)
            if (!_lastKnownRanking.TryGetValue(player.Id, out PlayerRanking previousRanking))
            {
                // First time seeing this player - initialize with their earliest known ranking
                // so we can detect promotions from their initial rank
                if (player.Rankings != null && player.Rankings.Any())
                {
                    var earliest = player.Rankings
                        .Where(r => !string.IsNullOrEmpty(r.Ranking))
                        .OrderBy(r => r.RankingDate ?? DateTimeOffset.MinValue)
                        .FirstOrDefault();
                    
                    if (earliest != null && !IsSameRanking(earliest, currentRanking))
                    {
                        previousRanking = earliest;
                        _lastKnownRanking[player.Id] = previousRanking;
                    }
                }
            }

            // Now check if current ranking differs from what we last knew
            if (previousRanking != null && !IsSameRanking(previousRanking, currentRanking))
            {
                // Only promotions from trusted organizations can trigger promotion bonus
                if (!currentRanking.IsTrustedOrganization)
                {
                    _lastKnownRanking[player.Id] = currentRanking;
                    return;
                }

                // Ranking changed - check if this is a promotion (higher rating for new ranking)
                int previousRankingRating = player.GetRatingByRanking(previousRanking, !isSgLeague);
                int currentRankingRating = player.GetRatingByRanking(currentRanking, !isSgLeague);

                if (currentRankingRating > previousRankingRating)
                {
                    // Calculate and apply rating floor immediately
                    // (promotion is already effective since ranking is now visible)
                    ApplyPromotionFloor(player, previousRanking, currentRanking, currentRankingRating, date, isSgLeague);
                }
            }

            // Update last known ranking
            _lastKnownRanking[player.Id] = currentRanking;
        }

        /// <summary>
        /// Applies promotion rating floor immediately for a player.
        /// </summary>
        private void ApplyPromotionFloor(ApplicationUser player, PlayerRanking previousRanking, 
            PlayerRanking currentRanking, int currentRankingRating, DateTimeOffset date, bool isSgLeague)
        {
            double ratingFloor;
            bool wasKyuPlayer = previousRanking.Ranking?.Contains('K', StringComparison.OrdinalIgnoreCase) ?? true;

            // Pro players and foreign players directly use their new ranking rating
            if (player.IsProPlayer || (!player.IsLocalPlayerAt(date) && !currentRanking.IsLocalRanking))
            {
                ratingFloor = currentRankingRating;
                wasKyuPlayer = false;
            }
            // Only apply promotion bonus for 4D and lower promotions
            // 5D and above (rating >= 2500) are considered strong enough and don't need the bonus
            else if (currentRankingRating < 2500)
            {
                // Rating floor is 50% of a single-rank difference below the new rank
                int singleRankDiff = Rating.RatingCalculator.GetSingleRankDifference(currentRankingRating);
                ratingFloor = currentRankingRating - singleRankDiff * 0.5;
            }
            else
            {
                return; // No bonus for 5D and above
            }

            // Apply floor if current rating is lower
            // Only apply bonus if we have a cached rating from processed matches
            // If no matches processed yet, skip the bonus - the player will enter with their
            // current ranking's rating, and any future promotions will be handled correctly
            if (!_dict.TryGetValue(player.Id, out var currentRating))
            {
                // No matches processed yet - don't apply promotion bonus
                // This prevents incorrect bonuses when "previousRanking" is actually from years ago
                return;
            }
            
            if (currentRating < ratingFloor)
            {
                double bonusAmount = ratingFloor - currentRating;
                _dict[player.Id] = ratingFloor;
                
                // Record the promotion bonus with the actual ranking date (not detection date)
                // This ensures the bonus is shown in the month when the promotion happened
                var promotionDate = currentRanking.RankingDate ?? date;
                if (!_promotionBonuses.TryGetValue(player.Id, out var bonusList))
                {
                    bonusList = new List<(DateTimeOffset, string, string, string, string, double)>();
                    _promotionBonuses[player.Id] = bonusList;
                }
                bonusList.Add((promotionDate, previousRanking.Ranking, previousRanking.Organization, 
                    currentRanking.Ranking, currentRanking.Organization, bonusAmount));
                
                // Only stop performance estimation for kyu players
                // Foreign dan players could be stronger than the promoted dan level
                if (wasKyuPlayer)
                {
                    _performanceTracker.Remove(player.Id);
                }
            }
        }

        /// <summary>
        /// Checks if two PlayerRanking objects represent the same ranking.
        /// </summary>
        private static bool IsSameRanking(PlayerRanking a, PlayerRanking b)
        {
            if (a == null || b == null)
                return a == b;
            return string.Equals(a.Ranking, b.Ranking, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.Organization, b.Organization, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks and applies promotions for all given players up to the specified date.
        /// This ensures players who got promoted but had no match get their bonus applied.
        /// Should be called before taking monthly snapshots.
        /// </summary>
        public void ApplyPromotionsUpToDate(IEnumerable<ApplicationUser> players, DateTimeOffset upToDate, bool isSgLeague)
        {
            // Simply check each player - promotions are applied immediately when detected
            foreach (var player in players)
            {
                CheckPlayerPromotion(player, upToDate, isSgLeague);
            }
        }

        /// <summary>
        /// Tracks a game result for a new foreign/unknown player and calculates their
        /// estimated initial rating after they've played enough games.
        /// After calculation, applies a correction to the player's current rating.
        /// </summary>
        private void TrackGameForPerformanceRating(ApplicationUser player, double opponentRating, double score, bool isSgLeague)
        {
            // Only track for players who need performance estimation
            if (!player.NeedDynamicFactor(!isSgLeague))
            {
                // Clean up tracker as we no longer need to track this player
                _performanceTracker.Remove(player.Id);
                return;
            }

            // Initialize tracking list if needed and add game result
            if (!_performanceTracker.TryGetValue(player.Id, out var games))
            {
                games = new List<(double opponentRating, double score)>();
                _performanceTracker[player.Id] = games;
            }
            games.Add((opponentRating, score));

            // After GAMES_FOR_ESTIMATION games, calculate estimated initial rating and apply correction
            if (games.Count == GAMES_FOR_ESTIMATION)
            {
                var estimatedInitialRating = CalculatePerformanceRating(games);
                player.EstimatedInitialRating = estimatedInitialRating;

                // Apply rating correction based on the difference between
                // original ranking-based rating and performance-based estimate
                if (_dict.TryGetValue(player.Id, out var currentRating))
                {
                    double originalInitialRating = player.GetRatingBeforeDate(player.FirstMatch.Date, !isSgLeague);
                    double ratingCorrection = estimatedInitialRating - originalInitialRating;
                    
                    // Apply partial correction (50%) to avoid over-adjusting
                    // The dynamic K-factor should have already moved them partway to their true rating
                    double correctedRating = currentRating + ratingCorrection * 0.5;
                    
                    _dict[player.Id] = correctedRating;

                    _performanceTracker.Remove(player.Id);
                }
            }
        }

        /// <summary>
        /// Gets and clears all promotion bonuses for a player up to (and including) a specific date.
        /// Returns list of (fromRanking, fromOrg, toRanking, toOrg, bonusAmount, promotionDate) tuples.
        /// </summary>
        public List<(string fromRanking, string fromOrg, string toRanking, string toOrg, double bonusAmount, DateTimeOffset? promotionDate)> 
            ConsumePromotionBonuses(string playerId, DateTimeOffset upToDate)
        {
            if (!_promotionBonuses.TryGetValue(playerId, out var bonusList))
                return new List<(string, string, string, string, double, DateTimeOffset?)>();

            var result = bonusList
                .Where(b => b.date <= upToDate)
                .Select(b => (b.fromRanking, b.fromOrg, b.toRanking, b.toOrg, b.bonusAmount, (DateTimeOffset?)b.date))
                .ToList();
            
            // Remove consumed bonuses
            bonusList.RemoveAll(b => b.date <= upToDate);
            
            return result;
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
                return Rating.RatingCalculator.DEFAULT_RATING;

            // Find the strongest opponent beaten and weakest opponent lost to
            double strongestWin = double.MinValue;
            double weakestLoss = double.MaxValue;
            double weightedOpponentSum = 0;
            double weightedScoreSum = 0;
            double weightSum = 0;

            foreach (var game in games)
            {
                // Opponent strength weight - stronger opponents give more information
                double weight = Math.Sqrt(Math.Max(1000, game.opponentRating) / 1000.0);
                
                weightedOpponentSum += game.opponentRating * weight;
                weightedScoreSum += game.score * weight;
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

        public virtual string GetResult(ApplicationUser user)
        {
            if (_dict.TryGetValue(user.Id, out var rating))
                return rating.ToString("F1");
            
            // Show ranking rating for users with no matches
            var rankingRating = user.GetRatingByRanking(user.GetCombinedRankingBeforeDate(League.CutoffDate));
            return rankingRating.ToString("F1");
        }

        public virtual string NameLocalizationKey => nameof(LocalizationKey.Elo);

        public virtual double this[ApplicationUser user]
        {
            get
            {
                if (_dict.TryGetValue(user.Id, out var rating))
                    return rating;
                
                // If user has no matches yet, use their latest ranking-based rating
                if (user.FirstMatch == DateTimeOffset.MinValue)
                    return user.GetRatingByRanking(user.LatestRanking);
                
                return user.GetRatingBeforeDate(user.FirstMatch.Date);
            }
            set => _dict[user.Id] = value;
        }
    }
}
