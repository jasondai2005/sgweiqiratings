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
            var rating2 = new Elo(firstPlayerRating, secondPlayerRating, firstUserScore, 1 - firstUserScore, Elo.GetK(firstPlayerRating) * factor2);

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
