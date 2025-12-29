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

        // Track which players received a performance correction (12th game adjustment only)
        // This is independent of promotion bonus logic
        private readonly HashSet<string> _playersWithCorrectionApplied = new HashSet<string>();

        // Number of games required before calculating estimated initial rating
        private const int GAMES_FOR_ESTIMATION = 12;

        // Static setting to control whether promotion bonus is enabled
        public static bool PromotionBonusEnabled { get; set; } = true;

        // Static setting to control SWA Only mode (skips TGA promotions)
        public static bool SwaOnly { get; set; } = false;

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

            bool isIntlLeague = match.League?.Name?.Contains("Intl.") ?? false;
            int firstPlayerRankingRating = match.FirstPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
            int secondPlayerRankingRating = match.SecondPlayer.GetRatingBeforeDate(match.Date.Date, isIntlLeague);
            double firstPlayerRating = _dict.TryGetValue(match.FirstPlayer.Id, out var cachedFirst) ? cachedFirst : firstPlayerRankingRating;
            double secondPlayerRating = _dict.TryGetValue(match.SecondPlayer.Id, out var cachedSecond) ? cachedSecond : secondPlayerRankingRating;

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
            }

            var rating = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, Elo.GetK(firstPlayerRating) * factor1);
            Elo specialRating = factor1 != factor2 ? 
                specialRating = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, Elo.GetK(secondPlayerRating) * factor2) :
                rating;

            match.OldFirstPlayerRating = firstPlayerRating.ToString("F1");
            match.OldSecondPlayerRating = secondPlayerRating.ToString("F1");

            _dict[match.FirstPlayer.Id] = rating.NewRatingAPlayer;
            _dict[match.SecondPlayer.Id] = specialRating.NewRatingBPlayer;

            // Check for promotions and queue rating floor adjustments (applied after all matches on that day)
            CheckForPromotion(match.FirstPlayer, match.Date, isIntlLeague);
            CheckForPromotion(match.SecondPlayer, match.Date, isIntlLeague);

            match.ShiftRating = rating.ShiftRatingAPlayer.ToString("F1");
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

        // Track the last known ranking for each player to detect promotions
        private readonly Dictionary<string, PlayerRanking> _lastKnownRanking = new Dictionary<string, PlayerRanking>();

        // Track pending promotion rating floors: player Id -> (rating floor, isIntlLeague, wasKyuPlayer)
        private readonly Dictionary<string, (double ratingFloor, bool isIntlLeague, bool wasKyuPlayer)> _pendingPromotionFloors 
            = new Dictionary<string, (double, bool, bool)>();

        // Track the current date being processed
        private DateTime _currentProcessingDate = DateTime.MinValue;

        /// <summary>
        /// Public method to check and apply promotion for a player at a specific date.
        /// Called when a player is first encountered to apply any promotions that may have happened.
        /// </summary>
        public void CheckPlayerPromotion(ApplicationUser player, DateTimeOffset date, bool isIntlLeague)
        {
            CheckForPromotion(player, date, isIntlLeague);
        }

        /// <summary>
        /// Checks if a player was promoted and queues a rating floor adjustment.
        /// The adjustment is applied after all matches on that day complete.
        /// </summary>
        private void CheckForPromotion(ApplicationUser player, DateTimeOffset matchDate, bool isIntlLeague)
        {
            if (player.IsVirtualPlayer)
                return;

            // Apply any pending promotion floors from previous days
            if (matchDate.Date > _currentProcessingDate)
            {
                ApplyPendingPromotionFloors();
                _currentProcessingDate = matchDate.Date;
            }

            // Get current ranking at match date
            var currentRanking = player.GetPlayerRankingBeforeDate(matchDate);
            if (currentRanking == null || string.IsNullOrEmpty(currentRanking.Ranking))
                return;

            // Skip if promotion bonus is disabled
            if (!PromotionBonusEnabled)
            {
                // Still track the ranking for when it's re-enabled
                _lastKnownRanking[player.Id] = currentRanking;
                return;
            }

            // Skip TGA promotions when SWA Only is enabled
            if (SwaOnly && currentRanking.Organization == "TGA")
            {
                // Don't track TGA rankings when SWA Only is enabled
                return;
            }

            // Check if ranking changed (promotion detected)
            PlayerRanking previousRanking;
            if (!_lastKnownRanking.TryGetValue(player.Id, out previousRanking))
            {
                // First time seeing this player - initialize with their earliest known ranking
                // so we can detect promotions from their initial rank
                // Rankings without dates are treated as earliest (MinValue)
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

            if (previousRanking != null && !IsSameRanking(previousRanking, currentRanking))
            {
                // Only local promotions (SWA/TGA) can trigger promotion bonus
                // Foreign rankings are not officially verified by local associations
                if (!currentRanking.IsLocalRanking)
                {
                    _lastKnownRanking[player.Id] = currentRanking;
                    return;
                }

                // Ranking changed - check if this is a promotion (higher rating for new ranking)
                int previousRankingRating = player.GetRatingByRanking(previousRanking, isIntlLeague);
                int currentRankingRating = player.GetRatingByRanking(currentRanking, isIntlLeague);

                if (currentRankingRating > previousRankingRating)
                {
                    // Only apply promotion bonus for 4D and lower promotions
                    // 5D and above (rating >= 2500) are considered strong enough and don't need the bonus
                    // Note: TGA (5D) = 2400 still gets bonus as it's equivalent to SWA 4D in strength
                    if (currentRankingRating < 2500)
                    {
                        // This is a promotion - queue rating floor for end of day
                        double ratingFloor = currentRankingRating - 50;
                        // Track if previous ranking was kyu - only kyu players should stop performance tracking
                        bool wasKyuPlayer = previousRanking.Ranking?.Contains('K', StringComparison.OrdinalIgnoreCase) ?? true;
                        _pendingPromotionFloors[player.Id] = (ratingFloor, isIntlLeague, wasKyuPlayer);
                    }
                }
            }

            // Update last known ranking
            _lastKnownRanking[player.Id] = currentRanking;
        }

        /// <summary>
        /// Checks if two PlayerRanking objects represent the same ranking.
        /// </summary>
        private static bool IsSameRanking(PlayerRanking a, PlayerRanking b)
        {
            if (a == null || b == null)
                return a == b;
            return string.Equals(a.Ranking, b.Ranking, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.Organization ?? "SWA", b.Organization ?? "SWA", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Applies all pending promotion rating floors.
        /// Called when the processing date changes or at the end of match processing.
        /// Also stops performance estimation tracking for kyu players who receive the bonus.
        /// Foreign dan players continue tracking as they could be stronger than the promoted level.
        /// </summary>
        private void ApplyPendingPromotionFloors()
        {
            foreach (var pending in _pendingPromotionFloors)
            {
                string playerId = pending.Key;
                double ratingFloor = pending.Value.ratingFloor;
                bool wasKyuPlayer = pending.Value.wasKyuPlayer;

                // Get current rating - either from _dict or initialize with rating floor
                double currentRating;
                if (!_dict.TryGetValue(playerId, out currentRating))
                {
                    // Player not in _dict yet - initialize with the rating floor
                    currentRating = 0;
                }

                if (currentRating < ratingFloor)
                {
                    _dict[playerId] = ratingFloor;
                    
                    // Only stop performance estimation for kyu players
                    // Foreign dan players could be stronger than the promoted dan level
                    if (wasKyuPlayer)
                    {
                        _performanceTracker.Remove(playerId);
                    }
                }
            }

            _pendingPromotionFloors.Clear();
        }

        /// <summary>
        /// Finalizes any pending operations. Should be called after all matches are processed.
        /// </summary>
        public void FinalizeProcessing()
        {
            ApplyPendingPromotionFloors();
        }

        /// <summary>
        /// Tracks a game result for a new foreign/unknown player and calculates their
        /// estimated initial rating after they've played enough games.
        /// After calculation, applies a correction to the player's current rating.
        /// </summary>
        private void TrackGameForPerformanceRating(ApplicationUser player, double opponentRating, double score, bool isIntlLeague)
        {
            // Only track for players who need performance estimation
            if (!player.NeedDynamicFactor(isIntlLeague))
            {
                // Clean up tracker as we no longer need to track this player
                _performanceTracker.Remove(player.Id);
                return;
            }

            // Skip virtual players (aggregated player pools like [China 5D])
            if (player.IsVirtualPlayer)
                return;

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
                    double originalInitialRating = player.GetRatingBeforeDate(player.FirstMatch.Date, isIntlLeague);
                    double ratingCorrection = estimatedInitialRating - originalInitialRating;
                    
                    // Apply partial correction (50%) to avoid over-adjusting
                    // The dynamic K-factor should have already moved them partway to their true rating
                    double correctedRating = currentRating + ratingCorrection * 0.5;
                    
                    _dict[player.Id] = correctedRating;
                    
                    // Track that this player received a correction
                    _playersWithCorrectionApplied.Add(player.Id);
                }
            }
        }

        /// <summary>
        /// Checks if a player received a performance correction (12th game adjustment) and clears the flag.
        /// This is independent of promotion bonus - only tracks performance-based corrections.
        /// </summary>
        public bool DidPlayerReceiveCorrection(string playerId)
        {
            return _playersWithCorrectionApplied.Remove(playerId);
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
            return _dict.TryGetValue(user.Id, out var rating) ? rating.ToString("F1") : "";
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

    public class EloStatChange : EloStat
    {
        public override string GetResult(ApplicationUser user)
        {
            return _dict.TryGetValue(user.Id, out var rating) ? (rating - user.GetRatingBeforeDate(League.CutoffDate)).ToString("F1") : "";
        }

        public override string NameLocalizationKey => nameof(LocalizationKey.Delta);

        public override double this[ApplicationUser user] => _dict.TryGetValue(user.Id, out var rating) ? rating : 0;
    }
}
