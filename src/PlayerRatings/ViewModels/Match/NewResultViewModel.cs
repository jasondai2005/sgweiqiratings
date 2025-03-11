using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PlayerRatings.ViewModels.Match
{
    public class NewResultViewModel
    {
        public NewResultViewModel()
        { }

        public NewResultViewModel(IEnumerable<Models.League> leagues, Dictionary<Models.ApplicationUser, IEnumerable<Guid>> users, DateTimeOffset? lastMatchTime, string matchName, double? factor)
        {
            Leagues = leagues;

            Users = users.OrderBy(x => x.Key.DisplayName).ToDictionary(y => y.Key, y => y.Value);

            Date = lastMatchTime == null ? DateTimeOffset.UtcNow : lastMatchTime.Value.AddSeconds(1);
            MatchName = matchName;
            Factor = factor ?? 1;
        }

        public IEnumerable<Models.League> Leagues { get; set; }

        public Dictionary<Models.ApplicationUser, IEnumerable<Guid>> Users { get; set; }

        [Required]
        public Guid LeagueId { get; set; }

        [Required]
        [DisplayFormat(ApplyFormatInEditMode = true)]
        [DataType(DataType.DateTime)]
        public DateTimeOffset Date { get; set; }

        [Required]
        public string FirstPlayerId { get; set; }

        [Required]
        public string SecondPlayerId { get; set; }

        [Range(0, int.MaxValue)]
        public int FirstPlayerScore { get; set; }

        [Range(0, int.MaxValue)]
        public int SecondPlayerScore { get; set; }

        public string MatchName { get; set; }

        [Range(0, 1)]
        public double? Factor { get; set; }
    }
}
