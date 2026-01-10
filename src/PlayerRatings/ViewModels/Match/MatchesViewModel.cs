using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayerRatings.ViewModels.Match
{
    public class MatchesViewModel
    {
        public MatchesViewModel(IEnumerable<Models.Match> matches, Guid leagueId, int pagesCount, int currentPage)
        {
            Matches = matches;
            LeagueId = leagueId;
            PagesCount = pagesCount;
            CurrentPage = currentPage;
            AvailableMonths = new List<DateTime>();
        }

        public MatchesViewModel(IEnumerable<Models.Match> matches, Guid leagueId, DateTime currentMonth, List<DateTime> availableMonths)
        {
            Matches = matches;
            LeagueId = leagueId;
            CurrentMonth = currentMonth;
            AvailableMonths = availableMonths ?? new List<DateTime>();
            PagesCount = 0;
            CurrentPage = 0;
        }

        public IEnumerable<Models.Match> Matches { get; private set; }

        public int PagesCount { get; private set; }

        public int CurrentPage { get; private set; }

        public Guid LeagueId { get; private set; }

        public DateTime CurrentMonth { get; private set; }

        public List<DateTime> AvailableMonths { get; private set; }

        public string CurrentMonthDisplay => CurrentMonth.ToString("MM/yyyy");

        public DateTime? PreviousMonth => AvailableMonths
            .Where(m => m < CurrentMonth)
            .OrderByDescending(m => m)
            .Cast<DateTime?>()
            .FirstOrDefault();

        public DateTime? NextMonth => AvailableMonths
            .Where(m => m > CurrentMonth)
            .OrderBy(m => m)
            .Cast<DateTime?>()
            .FirstOrDefault();
    }
}
