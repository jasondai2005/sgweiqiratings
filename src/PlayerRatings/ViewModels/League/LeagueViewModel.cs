using System;
using System.ComponentModel.DataAnnotations;

namespace PlayerRatings.ViewModels.League
{
    public class LeagueViewModel
    {
        public Guid Id { get; set; }

        [Required]
        public string Name { get; set; }

        public string CreatedByUserId { get; set; }

        public bool SupportProtectedRatings
        {
            get { return Engine.Rating.Elo.SwaRankedPlayersOnly; }
            set { Engine.Rating.Elo.SwaRankedPlayersOnly = value; }
        }
    }
}
