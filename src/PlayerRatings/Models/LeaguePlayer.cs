using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlayerRatings.Models
{
    /// <summary>
    /// Player status in a league.
    /// Stored as int in database (compatible with previous bool IsBlocked).
    /// </summary>
    public enum PlayerStatus
    {
        /// <summary>
        /// Normal player - shown in ratings, can participate in games.
        /// </summary>
        Normal = 0,
        
        /// <summary>
        /// Blocked player - excluded from ratings and cannot be selected for new matches.
        /// </summary>
        Blocked = 1,
        
        /// <summary>
        /// Hidden player - participates in games and ratings are calculated, 
        /// but not shown in current Rating list. Replaces InvisiblePlayers.
        /// </summary>
        Hidden = 2,
        
        /// <summary>
        /// Always shown player - will be shown in inactive player list even without any game record.
        /// </summary>
        AlwaysShown = 3
    }

    [Table("LeaguePlayer")]
    public class LeaguePlayer
    {
        public Guid Id { get; set; }

        /// <summary>
        /// Player status in this league.
        /// Stored in the IsBlocked column (0=Normal, 1=Blocked, 2=Hidden, 3=AlwaysShown).
        /// </summary>
        [Column("IsBlocked")]
        public PlayerStatus Status { get; set; } = PlayerStatus.Normal;

        /// <summary>
        /// Legacy property for backward compatibility. Maps to Status.
        /// </summary>
        [NotMapped]
        public bool IsBlocked
        {
            get => Status == PlayerStatus.Blocked;
            set => Status = value ? PlayerStatus.Blocked : PlayerStatus.Normal;
        }

        public string UserId { get; set; }

        public Guid LeagueId { get; set; }

        public virtual ApplicationUser User { get; set; }
        
        public virtual League League { get; set; }
    }
}
