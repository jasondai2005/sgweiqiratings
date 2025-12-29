using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PlayerRatings.Models;

namespace PlayerRatings.ViewModels.Player
{
    /// <summary>
    /// View model for editing player basic information
    /// </summary>
    public class EditPlayerInfoViewModel
    {
        public string PlayerId { get; set; }
        public Guid LeagueId { get; set; }
        
        [Display(Name = "Display Name")]
        [MaxLength(200)]
        public string DisplayName { get; set; }
        
        [Display(Name = "Birth Year")]
        [Range(1900, 2100, ErrorMessage = "Please enter a valid year")]
        public int? BirthYearValue { get; set; }
        
        [Display(Name = "Residence")]
        [MaxLength(100)]
        public string Residence { get; set; }
        
        [Display(Name = "Photo URL")]
        [MaxLength(500)]
        [Url(ErrorMessage = "Please enter a valid URL")]
        public string Photo { get; set; }
    }

    /// <summary>
    /// View model for adding/editing a player ranking
    /// </summary>
    public class EditRankingViewModel
    {
        public Guid? RankingId { get; set; }
        public string PlayerId { get; set; }
        public Guid LeagueId { get; set; }
        
        [Required(ErrorMessage = "Ranking is required")]
        [Display(Name = "Ranking")]
        [MaxLength(10)]
        [RegularExpression(@"^\d{1,2}[DKP]$", ErrorMessage = "Invalid ranking format. Use formats like 1D, 2K, 9P")]
        public string Ranking { get; set; }
        
        [Required(ErrorMessage = "Organization is required")]
        [Display(Name = "Organization")]
        [MaxLength(50)]
        public string Organization { get; set; }
        
        [Display(Name = "Ranking Date")]
        [DataType(DataType.Date)]
        public DateTime? RankingDate { get; set; }
        
        [Display(Name = "Note")]
        [MaxLength(200)]
        public string RankingNote { get; set; }
        
        /// <summary>
        /// Common organizations for dropdown
        /// </summary>
        public static List<string> CommonOrganizations => new List<string>
        {
            "SWA",
            "TGA",
            "CWA",
            "Nihon Kiin",
            "KBA",
            "AGA",
            "EGF",
            "Other"
        };
    }
}

