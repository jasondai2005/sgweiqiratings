using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlayerRatings.Models
{
    [Table("Match")]
    public class Match
    {
        public Guid Id { get; set; }

        public DateTimeOffset Date { get; set; }
        internal string StrDate => Date.ToString(ApplicationUser.DATE_FORMAT);

        public int FirstPlayerScore { get; set; }

        public int SecondPlayerScore { get; set; }

        public double? Factor { get; set; }

        public Guid LeagueId { get; set; }

        public string CreatedByUserId { get; set; }

        public string FirstPlayerId { get; set; }

        public string SecondPlayerId { get; set; }

        public virtual ApplicationUser FirstPlayer { get; set; }

        public virtual ApplicationUser SecondPlayer { get; set; }

        public virtual League League { get; set; }

        public string MatchName { get; set; }

        /// <summary>
        /// Foreign key to the tournament this match belongs to (null for standalone matches)
        /// </summary>
        public Guid? TournamentId { get; set; }

        /// <summary>
        /// Round number within the tournament (1 for R1, 2 for R2, etc.)
        /// </summary>
        public int? Round { get; set; }

        /// <summary>
        /// Navigation property to the tournament
        /// </summary>
        [ForeignKey("TournamentId")]
        public virtual Tournament Tournament { get; set; }

        /// <summary>
        /// Photo URL/path for the match (e.g., players playing)
        /// </summary>
        [MaxLength(500)]
        public string MatchPhoto { get; set; }

        /// <summary>
        /// Photo URL/path showing the match result
        /// </summary>
        [MaxLength(500)]
        public string MatchResultPhoto { get; set; }

        /// <summary>
        /// SGF or other game record data/URL
        /// </summary>
        public string GameRecord { get; set; }

        public virtual ApplicationUser CreatedByUser { get; set; }

        internal string OldFirstPlayerRating { get; set; }

        internal string OldSecondPlayerRating { get; set; }

        internal string ShiftRating { get; set; }

        public string GetDescription()
        {
            var firstPlayerName = FirstPlayer?.DisplayName ?? "BYE";
            var secondPlayerName = SecondPlayer?.DisplayName ?? "BYE";
            
            if (FirstPlayerScore > SecondPlayerScore)
            {
                return firstPlayerName + " - " + secondPlayerName;
            }

            return secondPlayerName + " - " + firstPlayerName;
        }

        public string GetScore()
        {
            if (FirstPlayerScore > SecondPlayerScore)
            {
                return FirstPlayerScore + " : " + SecondPlayerScore;
            }

            return SecondPlayerScore + " : " + FirstPlayerScore;
        }
    }
}
