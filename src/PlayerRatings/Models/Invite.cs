using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlayerRatings.Models
{
    [Table("Invite")]
    public class Invite
    {
        public Guid Id { get; set; }

        public DateTimeOffset CreatedOn { get; set; }

        public string InvitedById { get; set; }

        public virtual ApplicationUser InvitedBy { get; set; }

        public string CreatedUserId { get; set; }

        [ForeignKey("CreatedUserId")]
        public virtual ApplicationUser CreatedUser { get; set; }
    }
}
