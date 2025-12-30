using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlayerRatings.Models
{
    /// <summary>
    /// Represents a player's ranking at a specific point in time.
    /// </summary>
    [Table("PlayerRanking")]
    public class PlayerRanking
    {
        [Key]
        public Guid RankingId { get; set; }

        /// <summary>
        /// Foreign key to the player (AspNetUsers.Id)
        /// </summary>
        [Required]
        public string PlayerId { get; set; }

        /// <summary>
        /// Date when this ranking was achieved/recorded
        /// </summary>
        public DateTimeOffset? RankingDate { get; set; }

        /// <summary>
        /// The ranking grade (e.g., "30K", "1K", "1D", "5D", "9P")
        /// </summary>
        [Required]
        [MaxLength(10)]
        public string Ranking { get; set; }

        /// <summary>
        /// Organization that issued this ranking.
        /// Examples: "SWA" (Singapore Weiqi Association), "TGA" (Taiwan Go Association),
        /// "CWA" (China Weiqi Association), "Nihon Kiin", "KBA" (Korea Baduk Association), etc.
        /// </summary>
        [MaxLength(50)]
        public string Organization { get; set; }

        /// <summary>
        /// Optional note about this ranking (event name, certificate number, etc.)
        /// </summary>
        [MaxLength(200)]
        public string RankingNote { get; set; }

        /// <summary>
        /// Navigation property to the player
        /// </summary>
        [ForeignKey("PlayerId")]
        public virtual ApplicationUser Player { get; set; }

        /// <summary>
        /// Determines if this is a local (Singapore) ranking based on organization
        /// </summary>
        [NotMapped]
        public bool IsLocalRanking => Organization == "SWA" || Organization == "TGA";

        /// <summary>
        /// Determines if this is a foreign ranking
        /// </summary>
        [NotMapped]
        public bool IsForeignRanking => !IsLocalRanking;

        /// <summary>
        /// Organizations whose rankings are trusted.
        /// Trusted rankings are eligible for promotion bonus and kyu players can skip performance estimation.
        /// Includes local (SWA, TGA) and trusted foreign associations.
        /// </summary>
        private static readonly HashSet<string> TrustedOrganizations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SWA", "TGA", "MWA", "KBA", "Thailand", "Vietnam", "EGF"
        };

        /// <summary>
        /// Determines if this ranking is from a trusted organization.
        /// Trusted rankings: eligible for promotion bonus, kyu players can skip performance estimation.
        /// Organizations: SWA, TGA, MWA, KBA, Thailand, Vietnam, EGF
        /// </summary>
        [NotMapped]
        public bool IsTrustedOrganization => !string.IsNullOrEmpty(Organization) && TrustedOrganizations.Contains(Organization);
    }
}



