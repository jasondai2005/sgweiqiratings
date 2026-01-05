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
        // Tournament type constants
        public const string TypeCompetition = "Competition";
        public const string TypeSelection = "Selection";
        public const string TypeIntlSelection = "Intl Selection";  // International - selected players only (achievement)
        public const string TypeIntlOpen = "Intl Open";            // International - open to anyone (not achievement)
        public const string TypeTitle = "Title";                    // Title competition - winner gets title for 1 year
        
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
        /// Additional notes about the tournament
        /// </summary>
        public string Notes { get; set; }

        /// <summary>
        /// External links related to the tournament (multiple URLs separated by semicolons)
        /// </summary>
        public string ExternalLinks { get; set; }

        /// <summary>
        /// Photo URL/path for the tournament (similar to player photo)
        /// </summary>
        [MaxLength(500)]
        public string Photo { get; set; }

        /// <summary>
        /// Original standings photo URL/path (shown beside the Standings section)
        /// </summary>
        [MaxLength(500)]
        public string StandingsPhoto { get; set; }

        /// <summary>
        /// When true, calculate and save personal positions for players
        /// </summary>
        public bool SupportsPersonalAward { get; set; }

        /// <summary>
        /// When true, calculate and save team positions
        /// </summary>
        public bool SupportsTeamAward { get; set; }

        /// <summary>
        /// When true, calculate and save female player positions (requires SupportsPersonalAward)
        /// </summary>
        public bool SupportsFemaleAward { get; set; }

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
        /// Gets the full display name including ordinal, group, and start date (MM/yyyy)
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
                // Don't include Group for Title tournaments since it stores the title info
                if (!string.IsNullOrEmpty(Group) && !IsTitleTournament)
                    parts.Add(Group);
                if (StartDate.HasValue)
                    parts.Add(StartDate.Value.ToString("MM/yyyy"));
                return string.Join(" ", parts);
            }
        }
        
        /// <summary>
        /// Whether this is a Title tournament where winner gets a title for 1 year.
        /// </summary>
        [NotMapped]
        public bool IsTitleTournament => TournamentType == TypeTitle;
        
        /// <summary>
        /// Whether this is an International Selection tournament (achievement).
        /// </summary>
        [NotMapped]
        public bool IsIntlSelectionTournament => TournamentType == TypeIntlSelection;
        
        /// <summary>
        /// Gets the English title from Group field (format: "En Title - 中文头衔").
        /// Returns null if not a Title tournament or Group is not set.
        /// </summary>
        [NotMapped]
        public string TitleEn
        {
            get
            {
                if (!IsTitleTournament || string.IsNullOrEmpty(Group))
                    return null;
                var parts = Group.Split('-');
                return parts[0].Trim();
            }
        }
        
        /// <summary>
        /// Gets the Chinese title from Group field (format: "En Title - 中文头衔").
        /// Returns null if not a Title tournament or Group doesn't contain Chinese title.
        /// </summary>
        [NotMapped]
        public string TitleCn
        {
            get
            {
                if (!IsTitleTournament || string.IsNullOrEmpty(Group))
                    return null;
                var parts = Group.Split('-');
                return parts.Length > 1 ? parts[1].Trim() : null;
            }
        }
    }
}

