using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlayerRatings.Models
{
    [Table("League")]
    public class League
    {
        /// <summary>
        /// Only show ratings before this date
        /// </summary>
        internal static DateTimeOffset CutoffDate { get; set; }

        public Guid Id { get; set; }

        public string Name { get; set; }

        public string CreatedByUserId { get; set; }

        public ApplicationUser CreatedByUser { get; set; }

        public virtual ICollection<Match> Matches { get; set; }

        public virtual ICollection<Tournament> Tournaments { get; set; }
    }
}
