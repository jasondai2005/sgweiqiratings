using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PlayerRatings.ViewModels.Match
{
    public class HistoryRatingViewModel
    {
        public Guid LeagueId { get; set; }

        [Required]
        public string ByDate { get; set; }
    }
}
