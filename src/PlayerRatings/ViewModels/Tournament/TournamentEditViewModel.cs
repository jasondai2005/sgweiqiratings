using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PlayerRatings.ViewModels.Tournament
{
    /// <summary>
    /// View model for creating or editing a tournament
    /// </summary>
    public class TournamentEditViewModel
    {
        public Guid? Id { get; set; }
        
        [Required]
        public Guid LeagueId { get; set; }
        
        public string LeagueName { get; set; }
        
        [Required]
        [MaxLength(200)]
        [Display(Name = "Tournament Name")]
        public string Name { get; set; }
        
        [MaxLength(50)]
        [Display(Name = "Ordinal (e.g., '4th' or '2026')")]
        public string Ordinal { get; set; }
        
        [MaxLength(100)]
        [Display(Name = "Group")]
        public string Group { get; set; }
        
        [MaxLength(200)]
        [Display(Name = "Organizer")]
        public string Organizer { get; set; }
        
        [MaxLength(200)]
        [Display(Name = "Location")]
        public string Location { get; set; }
        
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }
        
        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }
        
        [MaxLength(100)]
        [Display(Name = "Tournament Type")]
        public string TournamentType { get; set; }
        
        [Display(Name = "Factor (leave empty for default)")]
        [Range(0, 10)]
        public double? Factor { get; set; }
        
        /// <summary>
        /// Common tournament types for dropdown
        /// </summary>
        public static readonly List<string> CommonTypes = new List<string>
        {
            "Competition",
            "Selection",
            "Certification",
            "League",
            "Friendly"
        };
        
        /// <summary>
        /// IDs of matches currently in this tournament
        /// </summary>
        public List<Guid> SelectedMatchIds { get; set; } = new List<Guid>();
        
        /// <summary>
        /// IDs of players in this tournament with their positions
        /// </summary>
        public List<TournamentPlayerEditModel> SelectedPlayers { get; set; } = new List<TournamentPlayerEditModel>();
        
        /// <summary>
        /// Month filter for match selection
        /// </summary>
        public int? FilterMonth { get; set; }
        
        /// <summary>
        /// Year filter for match selection
        /// </summary>
        public int? FilterYear { get; set; }
        
        /// <summary>
        /// Available matches for selection (populated by controller)
        /// </summary>
        public List<MatchSelectionItem> AvailableMatches { get; set; } = new List<MatchSelectionItem>();
        
        /// <summary>
        /// Available players for selection (populated by controller)
        /// </summary>
        public List<PlayerSelectionItem> AvailablePlayers { get; set; } = new List<PlayerSelectionItem>();
        
        /// <summary>
        /// All league players available to add to tournament (not already in tournament)
        /// </summary>
        public List<LeaguePlayerItem> LeaguePlayers { get; set; } = new List<LeaguePlayerItem>();
    }
    
    /// <summary>
    /// Model for editing a player's tournament participation
    /// </summary>
    public class TournamentPlayerEditModel
    {
        public string PlayerId { get; set; }
        
        public int? Position { get; set; }
        
        public Guid? PromotionId { get; set; }
    }
    
    /// <summary>
    /// Model for a match in the selection list
    /// </summary>
    public class MatchSelectionItem
    {
        public Guid Id { get; set; }
        
        public DateTimeOffset Date { get; set; }
        
        public string FirstPlayerName { get; set; }
        
        public string SecondPlayerName { get; set; }
        
        public int FirstPlayerScore { get; set; }
        
        public int SecondPlayerScore { get; set; }
        
        public string MatchName { get; set; }
        
        public double? Factor { get; set; }
        
        public bool IsSelected { get; set; }
        
        /// <summary>
        /// If the match already belongs to another tournament
        /// </summary>
        public Guid? CurrentTournamentId { get; set; }
        
        public string CurrentTournamentName { get; set; }
        
        public int? Round { get; set; }
    }
    
    /// <summary>
    /// Model for a player in the selection list
    /// </summary>
    public class PlayerSelectionItem
    {
        public string PlayerId { get; set; }
        
        public string PlayerName { get; set; }
        
        public string CurrentRanking { get; set; }
        
        public bool IsSelected { get; set; }
        
        public int? Position { get; set; }
        
        public Guid? PromotionId { get; set; }
        
        public string PromotionRanking { get; set; }
        
        /// <summary>
        /// Number of wins in this tournament
        /// </summary>
        public int Wins { get; set; }
        
        /// <summary>
        /// Number of losses in this tournament
        /// </summary>
        public int Losses { get; set; }
        
        /// <summary>
        /// SOS - Sum of Opponents' Scores (sum of opponents' wins)
        /// </summary>
        public int SOS { get; set; }
        
        /// <summary>
        /// SOSOS - Sum of Opponents' SOS (sum of opponents' SOS scores)
        /// </summary>
        public int SOSOS { get; set; }
        
        /// <summary>
        /// True if player is undefeated (0 losses) - champion status
        /// </summary>
        public bool IsUndefeated => Losses == 0 && Wins > 0;
    }
    
    /// <summary>
    /// A league player available to add to tournament
    /// </summary>
    public class LeaguePlayerItem
    {
        public string PlayerId { get; set; }
        public string PlayerName { get; set; }
    }
}

