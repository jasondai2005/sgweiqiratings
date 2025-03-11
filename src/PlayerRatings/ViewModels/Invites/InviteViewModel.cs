using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PlayerRatings.ViewModels.Invites
{
    public class InviteViewModel
    {
        public Guid? LeagueId { get; set; }

        [EmailAddress]
        public string Email { get; set; }

        public string DisplayName { get; set; }

        public string Ranking { get; set; }

        public Guid Id { get; set; }

        public IEnumerable<Models.League> Leagues { get; set; }
    }
}
