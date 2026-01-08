using System.Collections.Generic;
using System.Linq;
using PlayerRatings.Models;

namespace PlayerRatings.Services.Swiss
{
    /// <summary>
    /// Service for Swiss system tournament calculations.
    /// Extracted from TournamentsController for better reusability and testability.
    /// </summary>
    public class SwissSystemService : ISwissSystemService
    {
        /// <inheritdoc />
        public SwissStats CalculateSwissStats(IEnumerable<Match> matches)
        {
            var playerStats = new Dictionary<string, PlayerMatchStats>();
            var byeWins = new Dictionary<string, int>();

            foreach (var match in matches)
            {
                // Handle bye matches (opponent is NULL)
                if (string.IsNullOrEmpty(match.FirstPlayerId) || string.IsNullOrEmpty(match.SecondPlayerId))
                {
                    ProcessByeMatch(match, playerStats, byeWins);
                    continue;
                }

                ProcessRegularMatch(match, playerStats);
            }

            // Calculate SOS (Sum of Opponents' Scores)
            var sosScores = new Dictionary<string, double>();
            foreach (var player in playerStats)
            {
                sosScores[player.Key] = player.Value.Opponents.Sum(oppId =>
                    playerStats.TryGetValue(oppId, out var oppStats) ? oppStats.Wins : 0);
            }

            // Calculate SOSOS (Sum of Opponents' SOS)
            var sososScores = new Dictionary<string, double>();
            foreach (var player in playerStats)
            {
                sososScores[player.Key] = player.Value.Opponents.Sum(oppId =>
                    sosScores.TryGetValue(oppId, out var oppSos) ? oppSos : 0);
            }

            return new SwissStats 
            { 
                PlayerStats = playerStats, 
                SOS = sosScores, 
                SOSOS = sososScores 
            };
        }

        /// <inheritdoc />
        public Dictionary<string, int> CalculateSwissPositions(SwissStats stats)
        {
            var rankedPlayers = stats.PlayerStats
                .OrderByDescending(p => p.Value.Losses == 0 && p.Value.Wins > 0 ? 1 : 0) // Undefeated first
                .ThenByDescending(p => p.Value.Wins)
                .ThenByDescending(p => stats.SOS[p.Key])
                .ThenByDescending(p => stats.SOSOS[p.Key])
                .ThenByDescending(p => p.Value.PointsFor - p.Value.PointsAgainst)
                .ToList();

            var positions = new Dictionary<string, int>();
            int position = 1;
            int championsCount = 0;

            foreach (var player in rankedPlayers)
            {
                // Champions: undefeated with at least 1 win
                bool isChampion = player.Value.Losses == 0 && player.Value.Wins > 0;

                if (isChampion)
                {
                    positions[player.Key] = 1;
                    championsCount++;
                }
                else
                {
                    // Position accounts for champions at position 1
                    positions[player.Key] = championsCount > 0 ? position : position;
                }

                position++;
            }

            // Adjust positions: if 3 champions, next position should be 4
            if (championsCount > 0)
            {
                foreach (var player in positions.Keys.ToList())
                {
                    if (positions[player] > 1)
                    {
                        // Calculate true position (original position + champions - 1)
                        int originalPosition = rankedPlayers.FindIndex(p => p.Key == player) + 1;
                        positions[player] = originalPosition;
                    }
                }
            }

            return positions;
        }

        /// <inheritdoc />
        public Dictionary<string, int> CalculateSwissPositions(IEnumerable<Match> matches)
        {
            var stats = CalculateSwissStats(matches);
            return CalculateSwissPositions(stats);
        }

        private void ProcessByeMatch(Match match, Dictionary<string, PlayerMatchStats> playerStats, Dictionary<string, int> byeWins)
        {
            var realPlayerId = !string.IsNullOrEmpty(match.FirstPlayerId) ? match.FirstPlayerId : match.SecondPlayerId;
            if (string.IsNullOrEmpty(realPlayerId))
                return;

            if (!playerStats.ContainsKey(realPlayerId))
                playerStats[realPlayerId] = new PlayerMatchStats();

            var realPlayerScore = !string.IsNullOrEmpty(match.FirstPlayerId) ? match.FirstPlayerScore : match.SecondPlayerScore;
            var byeScore = !string.IsNullOrEmpty(match.FirstPlayerId) ? match.SecondPlayerScore : match.FirstPlayerScore;

            if (realPlayerScore > byeScore)
            {
                playerStats[realPlayerId].Wins++;
                byeWins[realPlayerId] = byeWins.GetValueOrDefault(realPlayerId) + 1;
            }
            else if (byeScore > realPlayerScore)
            {
                playerStats[realPlayerId].Losses++;
            }
        }

        private void ProcessRegularMatch(Match match, Dictionary<string, PlayerMatchStats> playerStats)
        {
            if (!playerStats.ContainsKey(match.FirstPlayerId))
                playerStats[match.FirstPlayerId] = new PlayerMatchStats();
            if (!playerStats.ContainsKey(match.SecondPlayerId))
                playerStats[match.SecondPlayerId] = new PlayerMatchStats();

            var stats1 = playerStats[match.FirstPlayerId];
            var stats2 = playerStats[match.SecondPlayerId];

            // Track opponents
            stats1.Opponents.Add(match.SecondPlayerId);
            stats2.Opponents.Add(match.FirstPlayerId);

            // Update points
            stats1.PointsFor += match.FirstPlayerScore;
            stats1.PointsAgainst += match.SecondPlayerScore;
            stats2.PointsFor += match.SecondPlayerScore;
            stats2.PointsAgainst += match.FirstPlayerScore;

            // Determine winner or draw
            if (match.FirstPlayerScore > match.SecondPlayerScore)
            {
                stats1.Wins++;
                stats2.Losses++;
            }
            else if (match.SecondPlayerScore > match.FirstPlayerScore)
            {
                stats1.Losses++;
                stats2.Wins++;
            }
            else
            {
                // Draw: award 0.5 points to each player
                stats1.Wins += 0.5;
                stats2.Wins += 0.5;
                stats1.Draws++;
                stats2.Draws++;
            }
        }
    }
}

