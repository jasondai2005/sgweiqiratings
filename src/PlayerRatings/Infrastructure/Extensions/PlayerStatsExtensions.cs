using System;
using System.Collections.Generic;
using System.Linq;
using PlayerRatings.ViewModels.Player;

namespace PlayerRatings.Infrastructure.Extensions
{
    /// <summary>
    /// Statistics for a specific time period.
    /// </summary>
    public class PeriodStats
    {
        public int Games { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public double WinRate => Games > 0 ? (double)Wins / Games * 100 : 0;
        public int ChampionshipCount { get; set; }
        public int TeamChampionshipCount { get; set; }
        public int FemaleChampionshipCount { get; set; }
    }

    /// <summary>
    /// Extension methods for calculating player statistics from game records.
    /// Consolidates computed properties from PlayerRatingHistoryViewModel.
    /// </summary>
    public static class PlayerStatsExtensions
    {
        /// <summary>
        /// Calculates statistics for all games.
        /// </summary>
        public static PeriodStats GetAllTimeStats(this IEnumerable<GameRecord> records)
        {
            var list = records?.ToList() ?? new List<GameRecord>();
            var rated = list.Where(g => g.Factor != 0).ToList();
            
            return new PeriodStats
            {
                Games = rated.Count,
                Wins = rated.Count(g => g.Result == "Win"),
                Losses = rated.Count(g => g.Result == "Loss"),
                ChampionshipCount = list.Where(g => g.TournamentPosition == 1)
                    .Select(g => g.TournamentId).Distinct().Count(),
                TeamChampionshipCount = list.Where(g => g.TeamPosition == 1)
                    .Select(g => g.TournamentId).Distinct().Count(),
                FemaleChampionshipCount = list.Where(g => g.FemalePosition == 1)
                    .Select(g => g.TournamentId).Distinct().Count()
            };
        }

        /// <summary>
        /// Calculates statistics for a specific year.
        /// </summary>
        public static PeriodStats GetYearStats(this IEnumerable<GameRecord> records, int year)
        {
            var list = records?.ToList() ?? new List<GameRecord>();
            var yearRecords = list.Where(g => g.Date.Year == year).ToList();
            var rated = yearRecords.Where(g => g.Factor != 0).ToList();
            
            return new PeriodStats
            {
                Games = rated.Count,
                Wins = rated.Count(g => g.Result == "Win"),
                Losses = rated.Count(g => g.Result == "Loss"),
                ChampionshipCount = yearRecords.Where(g => g.TournamentPosition == 1)
                    .Select(g => g.TournamentId).Distinct().Count(),
                TeamChampionshipCount = yearRecords.Where(g => g.TeamPosition == 1)
                    .Select(g => g.TournamentId).Distinct().Count(),
                FemaleChampionshipCount = yearRecords.Where(g => g.FemalePosition == 1)
                    .Select(g => g.TournamentId).Distinct().Count()
            };
        }

        /// <summary>
        /// Calculates statistics for the current year.
        /// </summary>
        public static PeriodStats GetCurrentYearStats(this IEnumerable<GameRecord> records)
            => GetYearStats(records, DateTime.Now.Year);

        /// <summary>
        /// Calculates statistics for the previous year.
        /// </summary>
        public static PeriodStats GetLastYearStats(this IEnumerable<GameRecord> records)
            => GetYearStats(records, DateTime.Now.Year - 1);

        /// <summary>
        /// Calculates statistics against a specific opponent.
        /// </summary>
        public static (int wins, int losses, double winRate) GetOpponentStats(
            this IEnumerable<GameRecord> records, string opponentId)
        {
            var list = records?
                .Where(g => g.Factor != 0 && g.OpponentId == opponentId)
                .ToList() ?? new List<GameRecord>();
            
            int wins = list.Count(g => g.Result == "Win");
            int losses = list.Count(g => g.Result == "Loss");
            int total = wins + losses;
            double winRate = total > 0 ? (double)wins / total * 100 : 0;
            
            return (wins, losses, winRate);
        }

        /// <summary>
        /// Gets the top opponents by number of games played.
        /// </summary>
        public static IEnumerable<(string opponentId, string opponentName, int games, int wins, int losses)> GetTopOpponents(
            this IEnumerable<GameRecord> records, int count = 10)
        {
            return records?
                .Where(g => g.Factor != 0 && !string.IsNullOrEmpty(g.OpponentId))
                .GroupBy(g => new { g.OpponentId, g.OpponentName })
                .Select(g => (
                    opponentId: g.Key.OpponentId,
                    opponentName: g.Key.OpponentName ?? "Unknown",
                    games: g.Count(),
                    wins: g.Count(r => r.Result == "Win"),
                    losses: g.Count(r => r.Result == "Loss")))
                .OrderByDescending(x => x.games)
                .Take(count) ?? Enumerable.Empty<(string, string, int, int, int)>();
        }

        /// <summary>
        /// Gets the rating trend (change over recent months).
        /// </summary>
        public static (double? change, string trend) GetRatingTrend(
            this IEnumerable<MonthlyRating> ratings, int months = 3)
        {
            var list = ratings?.OrderByDescending(r => r.Month).Take(months + 1).ToList();
            
            if (list == null || list.Count < 2)
                return (null, "stable");
            
            double latest = list.First().Rating;
            double oldest = list.Last().Rating;
            double change = latest - oldest;
            
            string trend = change > 50 ? "rising" : change < -50 ? "falling" : "stable";
            return (change, trend);
        }

        /// <summary>
        /// Gets the peak rating from monthly history.
        /// </summary>
        public static (double rating, int year, int month)? GetPeakRating(
            this IEnumerable<MonthlyRating> ratings)
        {
            var peak = ratings?.OrderByDescending(r => r.Rating).FirstOrDefault();
            if (peak == null)
                return null;
            return (peak.Rating, peak.Month.Year, peak.Month.Month);
        }

        /// <summary>
        /// Gets current win/loss streak.
        /// </summary>
        public static (int count, bool isWinStreak) GetCurrentStreak(
            this IEnumerable<GameRecord> records)
        {
            var list = records?
                .Where(g => g.Factor != 0)
                .OrderByDescending(g => g.Date)
                .ToList() ?? new List<GameRecord>();
            
            if (!list.Any())
                return (0, false);
            
            bool isWin = list.First().Result == "Win";
            int count = 0;
            
            foreach (var record in list)
            {
                if ((record.Result == "Win") == isWin)
                    count++;
                else
                    break;
            }
            
            return (count, isWin);
        }
    }
}

