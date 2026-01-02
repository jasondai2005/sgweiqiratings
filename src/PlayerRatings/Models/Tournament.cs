using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlayerRatings.Models
{
    /// <summary>
    /// Represents a tournament within a league.
    /// A tournament is a middle layer between League and Match.
    /// </summary>
    [Table("Tournament")]
    public class Tournament
    {
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Foreign key to the league this tournament belongs to
        /// </summary>
        [Required]
        public Guid LeagueId { get; set; }

        /// <summary>
        /// Name of the tournament (e.g., "SWA Dan Cert", "TGA Open")
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        /// <summary>
        /// Ordinal value like "4th" or "2026" (as year)
        /// </summary>
        [MaxLength(50)]
        public string Ordinal { get; set; }

        /// <summary>
        /// Group name within the tournament (e.g., "Group A", "Dan Group")
        /// </summary>
        [MaxLength(100)]
        public string Group { get; set; }

        /// <summary>
        /// Organization or club hosting the tournament
        /// </summary>
        [MaxLength(200)]
        public string Organizer { get; set; }

        /// <summary>
        /// Location where the tournament is held
        /// </summary>
        [MaxLength(200)]
        public string Location { get; set; }

        /// <summary>
        /// Start date of the tournament (stored as beginning of day)
        /// </summary>
        public DateTimeOffset? StartDate { get; set; }

        /// <summary>
        /// End date of the tournament (stored as end of day)
        /// </summary>
        public DateTimeOffset? EndDate { get; set; }

        /// <summary>
        /// Type of tournament: Competition, Selection, or custom specified type
        /// </summary>
        [MaxLength(100)]
        public string TournamentType { get; set; }

        /// <summary>
        /// Default factor for matches in this tournament (null means use match default)
        /// </summary>
        public double? Factor { get; set; }

        /// <summary>
        /// Navigation property to the league
        /// </summary>
        [ForeignKey("LeagueId")]
        public virtual League League { get; set; }

        /// <summary>
        /// Navigation property to tournament players
        /// </summary>
        public virtual ICollection<TournamentPlayer> TournamentPlayers { get; set; }

        /// <summary>
        /// Navigation property to matches in this tournament
        /// </summary>
        public virtual ICollection<Match> Matches { get; set; }

        /// <summary>
        /// Gets the full display name including ordinal and group
        /// </summary>
        [NotMapped]
        public string FullName
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(Ordinal))
                    parts.Add(Ordinal);
                parts.Add(Name);
                if (!string.IsNullOrEmpty(Group))
                    parts.Add(Group);
                return string.Join(" ", parts);
            }
        }
    }
}

