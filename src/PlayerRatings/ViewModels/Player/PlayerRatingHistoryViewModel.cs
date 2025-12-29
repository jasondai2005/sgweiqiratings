using System;
using System.Collections.Generic;
using PlayerRatings.Models;

namespace PlayerRatings.ViewModels.Player
{
    public class PlayerRatingHistoryViewModel
    {
        public ApplicationUser Player { get; set; }
        public Guid LeagueId { get; set; }
        public List<MonthlyRating> MonthlyRatings { get; set; } = new List<MonthlyRating>();
        public List<GameRecord> GameRecords { get; set; } = new List<GameRecord>();
        public bool SwaOnly { get; set; }
        public bool IsIntlLeague { get; set; }
        public bool PromotionBonus { get; set; }
    }

    public class MonthlyRating
    {
        public DateTime Month { get; set; }
        public string MonthDisplay => Month.ToString("MMM yyyy");
        public double Rating { get; set; }
        public string RatingDisplay => Rating.ToString("F1");
        public int MatchesInMonth { get; set; }
        public List<string> MatchNames { get; set; } = new List<string>();
    }

    public class GameRecord
    {
        public DateTimeOffset Date { get; set; }
        public string DateDisplay => Date.ToString("dd/MM/yyyy");
        public string MatchName { get; set; }
        public string OpponentName { get; set; }
        public string OpponentId { get; set; }
        public string Result { get; set; }
        public double PlayerRating { get; set; }
        public double OpponentRating { get; set; }
        public string RatingDisplay => $"{PlayerRating.ToString("F1")} - {OpponentRating.ToString("F1")}";
    }
}

