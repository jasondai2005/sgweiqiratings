using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PlayerRatings.ViewModels.Player;

namespace PlayerRatings.ViewModels.Invites
{
    public class InviteViewModel
    {
        public Guid? LeagueId { get; set; }

        [EmailAddress]
        public string Email { get; set; }

        public string DisplayName { get; set; }

        public string Ranking { get; set; }

        [Display(Name = "Ranking Organization")]
        public string RankingOrganization { get; set; }

        [Display(Name = "Ranking Date")]
        [DataType(DataType.Date)]
        public DateTime? RankingDate { get; set; }

        [Display(Name = "Residence")]
        public string Residence { get; set; }

        [Display(Name = "Birth Year")]
        [Range(1900, 2100, ErrorMessage = "Please enter a valid year")]
        public int? BirthYearValue { get; set; }

        [Display(Name = "Photo URL")]
        [Url(ErrorMessage = "Please enter a valid URL")]
        public string PhotoUrl { get; set; }

        public Guid Id { get; set; }

        public IEnumerable<Models.League> Leagues { get; set; }

        /// <summary>
        /// Common organizations for dropdown
        /// </summary>
        public static List<string> CommonOrganizations => EditRankingViewModel.CommonOrganizations;
    }
}
