using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlayerRatings.Models
{
    /// <summary>
    /// Represents a player's participation in a tournament.
    /// </summary>
    [Table("TournamentPlayer")]
    public class TournamentPlayer
    {
        /// <summary>
        /// Foreign key to the tournament
        /// </summary>
        [Required]
        public Guid TournamentId { get; set; }

        /// <summary>
        /// Foreign key to the player (AspNetUsers.Id)
        /// </summary>
        [Required]
        [MaxLength(450)]
        public string PlayerId { get; set; }

        /// <summary>
        /// Final position in the tournament (1st, 2nd, etc.)
        /// Multiple players can share the same position.
        /// Null means position not yet determined.
        /// </summary>
        public int? Position { get; set; }

        /// <summary>
        /// Foreign key to PlayerRanking for promotion achieved in this tournament
        /// </summary>
        public Guid? PromotionId { get; set; }

        /// <summary>
        /// Team name the player belongs to in this tournament
        /// </summary>
        [MaxLength(200)]
        public string Team { get; set; }

        /// <summary>
        /// Team position (for top 3 players of a team when SupportsTeamAward is true)
        /// </summary>
        public int? TeamPosition { get; set; }

        /// <summary>
        /// Female position (separate ranking for female players when SupportsFemaleAward is true)
        /// </summary>
        public int? FemalePosition { get; set; }

        /// <summary>
        /// Photo URL/path for this player in this tournament
        /// </summary>
        [MaxLength(500)]
        public string Photo { get; set; }

        /// <summary>
        /// Team photo URL/path for when this player won team award
        /// </summary>
        [MaxLength(500)]
        public string TeamPhoto { get; set; }

        /// <summary>
        /// Female award photo URL/path for when this player won female award
        /// </summary>
        [MaxLength(500)]
        public string FemaleAwardPhoto { get; set; }

        /// <summary>
        /// Navigation property to the tournament
        /// </summary>
        [ForeignKey("TournamentId")]
        public virtual Tournament Tournament { get; set; }

        /// <summary>
        /// Navigation property to the player
        /// </summary>
        [ForeignKey("PlayerId")]
        public virtual ApplicationUser Player { get; set; }

        /// <summary>
        /// Navigation property to the promotion ranking achieved
        /// </summary>
        [ForeignKey("PromotionId")]
        public virtual PlayerRanking Promotion { get; set; }
    }
}

