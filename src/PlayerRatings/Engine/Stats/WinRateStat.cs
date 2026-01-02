using PlayerRatings.Localization;
using PlayerRatings.Models;
using System.Collections.Generic;

namespace PlayerRatings.Engine.Stats
{
    public class WinRateStat : IStat
    {
        private readonly Dictionary<string, int> _wins = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _total = new Dictionary<string, int>();

        public void AddMatch(Match match)
        {
            // Skip bye matches (either player is NULL)
            if (match.FirstPlayer == null || match.SecondPlayer == null)
                return;
                
            if (match.Factor.HasValue && match.Factor.Value == 0)
                return;

            _wins[match.FirstPlayer.Id] = _wins.ContainsKey(match.FirstPlayer.Id) ? _wins[match.FirstPlayer.Id] : 0;
            _wins[match.SecondPlayer.Id] = _wins.ContainsKey(match.SecondPlayer.Id) ? _wins[match.SecondPlayer.Id] : 0;
            _total[match.FirstPlayer.Id] = _total.ContainsKey(match.FirstPlayer.Id) ? _total[match.FirstPlayer.Id] : 0;
            _total[match.SecondPlayer.Id] = _total.ContainsKey(match.SecondPlayer.Id) ? _total[match.SecondPlayer.Id] : 0;

            _total[match.FirstPlayer.Id]++;
            _total[match.SecondPlayer.Id]++;

            if (match.FirstPlayerScore == match.SecondPlayerScore)
            {
                return;
            }

            if (match.FirstPlayerScore > match.SecondPlayerScore)
            {
                _wins[match.FirstPlayer.Id]++;
            }
            else
            {
                _wins[match.SecondPlayer.Id]++;
            }
        }

        public string GetResult(ApplicationUser user)
        {
            try
            {
                return string.Format("{0} ({1}/{2})", ((double)_wins[user.Id] / _total[user.Id]).ToString("P01"), _wins[user.Id], _total[user.Id]);
            }
            catch
            {
                return String.Empty;
            }
        }

        public string NameLocalizationKey { get; } = nameof(LocalizationKey.WinRate);
    }
}
