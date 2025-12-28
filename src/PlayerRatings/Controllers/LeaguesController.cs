using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayerRatings.Engine.Rating;
using PlayerRatings.Engine.Stats;
using PlayerRatings.Models;
using PlayerRatings.Repositories;
using PlayerRatings.Util;
using PlayerRatings.ViewModels.League;
using PlayerRatings.ViewModels.Player;

namespace PlayerRatings.Controllers
{
    [Authorize]
    public class LeaguesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILeaguesRepository _leaguesRepository;

        public LeaguesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            ILeaguesRepository leaguesRepository)
        {
            _context = context;
            _userManager = userManager;
            _leaguesRepository = leaguesRepository;
        }

        // GET: Leagues
        public async Task<IActionResult> Index()
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            return
                View(_leaguesRepository.GetLeagues(currentUser).ToList().Select(l => new LeagueViewModel
                {
                    Id = l.Id,
                    Name = l.Name,
                    CreatedByUserId = l.CreatedByUserId
                }));
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var league = _leaguesRepository.GetUserAuthorizedLeague(currentUser, id);

            if (league == null)
            {
                return NotFound();
            }

            var players = _context.LeaguePlayers.Include(lp => lp.User).Where(lp =>
                lp.LeagueId == league.Id &&
                !lp.User.DisplayName.Contains("[") &&
                lp.User.DisplayName != "Admin").ToList();
            if (Elo.SwaRankedPlayersOnly)
                players = players.Where(x => x.User.LatestSwaRanking.Any()).ToList();
            players.Sort(CompareByRankingAndName);
            return View(new LeagueDetailsViewModel
            {
                League = league,
                Players = players,
                SwaRankedPlayersOnly = Elo.SwaRankedPlayersOnly
            });
        }

        public IActionResult NoLeagues()
        {
            return View();
        }

        // GET: Leagues/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Leagues/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LeagueViewModel model)
        {
            if (ModelState.IsValid)
            {
                var currentUser = await User.GetApplicationUser(_userManager);

                var league = new League
                {
                    Id = Guid.NewGuid(),
                    Name = model.Name,
                    CreatedByUser = currentUser
                };
                _context.League.Add(league);
                _context.LeaguePlayers.Add(new LeaguePlayer
                {
                    Id = Guid.NewGuid(),
                    League = league,
                    User = currentUser
                });
                _context.SaveChanges();

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: Leagues/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUser = await User.GetApplicationUser(_userManager);

            var league = _leaguesRepository.GetAdminAuthorizedLeague(currentUser, id.Value);
            if (league == null)
            {
                return NotFound();
            }
            return View(new LeagueViewModel
            {
                Id = league.Id,
                CreatedByUserId = league.CreatedByUserId,
                Name = league.Name
            });
        }

        // POST: Leagues/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(LeagueViewModel model)
        {
            if (ModelState.IsValid)
            {
                var currentUser = await User.GetApplicationUser(_userManager);

                var league = _leaguesRepository.GetAdminAuthorizedLeague(currentUser, model.Id);
                if (league == null)
                {
                    return NotFound();
                }

                league.Name = model.Name;

                _context.Update(league);
                _context.SaveChanges();

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: Leagues/Delete/5
        [ActionName("Delete")]
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUser = await User.GetApplicationUser(_userManager);

            var league = _leaguesRepository.GetAdminAuthorizedLeague(currentUser, id.Value);
            if (league == null)
            {
                return NotFound();
            }

            return View(new LeagueViewModel
            {
                Id = league.Id,
                Name = league.Name,
                CreatedByUserId = league.CreatedByUserId
            });
        }

        // POST: Leagues/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var league = _leaguesRepository.GetAdminAuthorizedLeague(currentUser, id);
            if (league == null)
            {
                return NotFound();
            }

            _context.League.Remove(league);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetBlocked(Guid playerId, bool block)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var player = _context.LeaguePlayers.SingleOrDefault(lp => lp.Id == playerId);

            if (player == null)
            {
                return NotFound();
            }

            var league = _leaguesRepository.GetAdminAuthorizedLeague(currentUser, player.LeagueId);
            if (league == null)
            {
                return NotFound();
            }

            player.IsBlocked = block;

            _context.SaveChanges();

            var id = league.Id;
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetProtectedRatingsOption(bool supportProtectedRatings, Guid id)
        {
            Elo.SwaRankedPlayersOnly = supportProtectedRatings;
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Leagues/Rating/5
        private EloStat elo = new();
        public async Task<IActionResult> Rating(Guid? id, string byDate, bool swaOnly = false)
        {
            if (id == null)
            {
                return NotFound();
            }
            var date = byDate == null ? DateTimeOffset.Now : DateTimeOffset.ParseExact(byDate, "dd/MM/yyyy", null);
            League.CutoffDate = date;
            var currentUser = await User.GetApplicationUser(_userManager);

            var league = _leaguesRepository.GetUserAuthorizedLeague(currentUser, id.Value);

            if (league == null)
            {
                return NotFound();
            }

            league =
                _context.League.Include(l => l.Matches)
                    .ThenInclude(m => m.FirstPlayer)
                    .Include(l => l.Matches)
                    .ThenInclude(m => m.SecondPlayer)
                    .Single(m => m.Id == id);

            var notBlockedUserIds =
                new HashSet<string>(
                    _context.LeaguePlayers.Where(lp => lp.LeagueId == league.Id && !lp.IsBlocked)
                        .Select(lp => lp.UserId));
            var activeUsers = new HashSet<ApplicationUser>();
            elo = new EloStat();
            var stats = new List<IStat>
            {
                elo,
                new WinRateStat()
            };
            if (byDate != null && date.Year < 2024)
                stats.Add(new EloStatChange());

            // Filter matches based on swaOnly toggle
            var matches = swaOnly 
                ? league.Matches.Where(x => x.Date <= date && x.MatchName.Contains("SWA ")).OrderBy(m => m.Date)
                : league.Matches.Where(x => x.Date <= date && (x.MatchName.Contains("SWA ") || x.MatchName.Contains("TGA ") || x.MatchName.Contains("SG "))).OrderBy(m => m.Date);
            foreach (var match in matches)
            {
                if (notBlockedUserIds.Contains(match.FirstPlayerId))
                {
                    AddUser(activeUsers, match, match.FirstPlayer);
                }
                if (notBlockedUserIds.Contains(match.SecondPlayerId))
                {
                    AddUser(activeUsers, match, match.SecondPlayer);
                }

                foreach (var stat in stats)
                {
                    stat.AddMatch(match);
                }
            }

            var userList = new List<ApplicationUser>(activeUsers);
            var forecast = new Dictionary<string, Dictionary<string, string>>();
            foreach (var appUser in userList)
            {
                var dict = new Dictionary<string, string>();

                foreach (var t in userList)
                {
                    var userRating = elo[appUser];
                    dict[t.Id] = (new Elo(userRating, elo[t], 1, 0).NewRatingAPlayer - userRating).ToString("N1");
                }

                forecast[appUser.Id] = dict;
            }

            var lastMatches = matches.Where(m=> m.Date > date.AddMonths(-1));
            // Players in monitoring period shouldn't impact existing players' positions
            var users = activeUsers.Where(x => league.Name.Contains("Intl.") || (!x.IsHiddenPlayer)).ToList();
            users.Sort(CompareByRatingAndName);

            var promotedPlayers = activeUsers.Where(x => (date.Year > 2023 || x.IsHiddenPlayer) && x.Promotion.Contains('→')).ToList();
            promotedPlayers.Sort(CompareByRankingRatingAndName);

            return View(new RatingViewModel(stats, users, promotedPlayers, lastMatches, forecast, id.Value, byDate, swaOnly));
        }

        private static void AddUser(HashSet<ApplicationUser> activeUsers, Match match, ApplicationUser player)
        {
            if (player.FirstMatch == DateTimeOffset.MinValue)
            {
                player.FirstMatch = match.Date;
            }

            player.LastMatch = match.Date;
                player.MatchCount++;

            activeUsers.Add(player);
        }

        private int CompareByRatingAndName(ApplicationUser x, ApplicationUser y)
        {
            if (x.IsVirtualPlayer && !y.IsVirtualPlayer)
                return 1;
            if (!x.IsVirtualPlayer && y.IsVirtualPlayer)
                return -1;

            if (!x.Active && y.Active)
                return 1;
            if (x.Active && !y.Active)
                return -1;

            var ranking1 = x.GetRankingBeforeDate(League.CutoffDate);
            var ranking2 = y.GetRankingBeforeDate(League.CutoffDate);
            return CompareRatings(x, y, ranking1, ranking2);
        }

        private int CompareByRankingRatingAndName(ApplicationUser x, ApplicationUser y)
        {
            var ranking1 = x.GetRankingBeforeDate(League.CutoffDate);
            var ranking2 = y.GetRankingBeforeDate(League.CutoffDate);
            var rankingRating1 = x.GetRatingByRanking(ranking1);
            var rankingRating2 = y.GetRatingByRanking(ranking2);

            if (rankingRating1 == rankingRating2)
            {
                if (ranking1 == ranking2)
                    return CompareRatings(x, y, ranking1, ranking2);
                else
                    return ranking1.CompareTo(ranking2); // higher ranking first
            }
            else
            {
                return rankingRating1.CompareTo(rankingRating2) * -1; // higher rating first
            }
        }

        private int CompareRatings(ApplicationUser x, ApplicationUser y, string ranking1, string ranking2)
        {
            double rating1 = double.Parse(elo.GetResult(x));
            double rating2 = double.Parse(elo.GetResult(y));
            
            if (rating1 == rating2) // the same rating
            {
                if (ranking1 == ranking2)
                {
                    return x.DisplayName.CompareTo(y.DisplayName);
                }
                else
                {
                    if (ranking1 == null)
                        return 1;
                    if (ranking2 == null)
                        return -1;
                    return ranking1.CompareTo(ranking2); // higher ranking first
                }
            }
            else
            {
                return rating1.CompareTo(rating2) * -1; // higher rating first
            }
        }

        private int CompareByRankingAndName(LeaguePlayer x, LeaguePlayer y)
        {
            var user1 = x.User;
            var user2 = y.User;
            int rankingRating1 = 0;
            int rankingRating2 = 0;
            var user1Ranking = Elo.SwaRankedPlayersOnly ? user1.LatestSwaRanking : user1.LatestRanking;
            var user2Ranking = Elo.SwaRankedPlayersOnly ? user2.LatestSwaRanking : user2.LatestRanking;
            if (!string.IsNullOrEmpty(user1Ranking) && (!user1Ranking.Contains('?') || user1Ranking.Contains('D')))
                rankingRating1 = user1.GetRatingByRanking(user1Ranking);
            if (!string.IsNullOrEmpty(user2Ranking) && (!user2Ranking.Contains('?') || user2Ranking.Contains('D')))
                rankingRating2 = user2.GetRatingByRanking(user2Ranking);

            if (rankingRating1 == rankingRating2) // the same rating
            {
                var latestRanking1 = ApplicationUser.GetEffectiveRanking(user1Ranking);
                var latestRanking2 = ApplicationUser.GetEffectiveRanking(user2Ranking);
                if (rankingRating1 <= 1710) // will not differenciate kyus certified by SWA or other
                {
                    latestRanking1 = latestRanking1.Trim('(', ')');
                    latestRanking2 = latestRanking2.Trim('(', ')');
                }
                if (rankingRating1 == 0)
                {
                    latestRanking1 = latestRanking2 = string.Empty;
                }

                if (latestRanking1 == latestRanking2)
                {
                    return user1.DisplayName.CompareTo(user2.DisplayName);
                }
                else
                {
                    if (latestRanking1.Contains("5D") || latestRanking1.Contains("6D"))
                        return latestRanking2.CompareTo(latestRanking1); // the same ranking, singpore ranking first
                    else
                        return latestRanking1.CompareTo(latestRanking2); // higher ranking first
                }
            }
            else
                return rankingRating1.CompareTo(rankingRating2) * -1; // higher rating first
        }

        // GET: Leagues/Player/5?playerId=xxx
        public async Task<IActionResult> Player(Guid id, string playerId, bool swaOnly = false)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return NotFound();
            }

            var currentUser = await User.GetApplicationUser(_userManager);
            var league = _leaguesRepository.GetUserAuthorizedLeague(currentUser, id);

            if (league == null)
            {
                return NotFound();
            }

            league = _context.League
                .Include(l => l.Matches).ThenInclude(m => m.FirstPlayer)
                .Include(l => l.Matches).ThenInclude(m => m.SecondPlayer)
                .Single(m => m.Id == id);

            var player = await _userManager.FindByIdAsync(playerId);
            if (player == null)
            {
                return NotFound();
            }

            // Find player's matches based on swaOnly filter
            var playerMatches = swaOnly
                ? league.Matches
                    .Where(m => (m.FirstPlayerId == playerId || m.SecondPlayerId == playerId) &&
                           m.MatchName.Contains("SWA "))
                    .OrderBy(m => m.Date)
                    .ToList()
                : league.Matches
                    .Where(m => (m.FirstPlayerId == playerId || m.SecondPlayerId == playerId) &&
                           (m.MatchName.Contains("SWA ") || m.MatchName.Contains("TGA ") || m.MatchName.Contains("SG ")))
                    .OrderBy(m => m.Date)
                    .ToList();

            if (!playerMatches.Any())
            {
                return NotFound();
            }

            var firstMatchDate = playerMatches.First().Date;
            
            // For new players with foreign/unknown ranking, start from when rating is corrected (after 12 games)
            // This is because early ratings are unreliable during the estimation period
            var initialRanking = player.Ranking ?? "";
            bool isNewForeignPlayer = initialRanking.Contains('[') && !initialRanking.Contains(' ');
            
            DateTimeOffset startDate = firstMatchDate;
            if (isNewForeignPlayer && playerMatches.Count >= 12)
            {
                // Start from the month after the 12th game (when correction is applied)
                startDate = playerMatches[11].Date; // 12th game (0-indexed)
            }
            
            var monthlyRatings = new List<MonthlyRating>();

            // Calculate rating at the beginning of each month from start date to now
            var currentMonth = new DateTime(startDate.Year, startDate.Month, 1).AddMonths(1);
            var endMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(1);

            while (currentMonth <= endMonth)
            {
                // Calculate rating up to the first day of this month
                League.CutoffDate = new DateTimeOffset(currentMonth);
                var eloStat = new EloStat();

                var matchesUpToDate = swaOnly
                    ? league.Matches
                        .Where(x => x.Date < League.CutoffDate && x.MatchName.Contains("SWA "))
                        .OrderBy(m => m.Date)
                        .ToList()
                    : league.Matches
                        .Where(x => x.Date < League.CutoffDate && 
                               (x.MatchName.Contains("SWA ") || x.MatchName.Contains("TGA ") || x.MatchName.Contains("SG ")))
                        .OrderBy(m => m.Date)
                        .ToList();

                // Check if player has any matches before this date
                var playerMatchesUpToDate = matchesUpToDate
                    .Where(m => m.FirstPlayerId == playerId || m.SecondPlayerId == playerId)
                    .ToList();
                bool playerHasMatches = playerMatchesUpToDate.Any();
                
                // For new foreign players, only show rating after 12 games (when correction is applied)
                if (isNewForeignPlayer && playerMatchesUpToDate.Count < 12)
                {
                    playerHasMatches = false;
                }
                
                int matchesInMonth = 0;
                foreach (var match in matchesUpToDate)
                {
                    // Track player's first match for proper initialization
                    if (match.FirstPlayer.FirstMatch == DateTimeOffset.MinValue)
                        match.FirstPlayer.FirstMatch = match.Date;
                    if (match.SecondPlayer.FirstMatch == DateTimeOffset.MinValue)
                        match.SecondPlayer.FirstMatch = match.Date;

                    match.FirstPlayer.MatchCount++;
                    match.SecondPlayer.MatchCount++;

                    eloStat.AddMatch(match);

                    // Count matches in this specific month
                    if ((match.FirstPlayerId == playerId || match.SecondPlayerId == playerId) &&
                        match.Date.Year == currentMonth.Year && match.Date.Month == currentMonth.Month)
                    {
                        matchesInMonth++;
                    }
                }

                // Only add rating if player has played matches before this date
                if (playerHasMatches)
                {
                    // Get the player's rating - safe now because player has matches
                    double rating = eloStat[player];

                    monthlyRatings.Add(new MonthlyRating
                    {
                        Month = currentMonth,
                        Rating = rating,
                        MatchesInMonth = matchesInMonth
                    });
                }

                // Reset match counts for next iteration
                foreach (var match in matchesUpToDate)
                {
                    match.FirstPlayer.MatchCount = 0;
                    match.FirstPlayer.FirstMatch = DateTimeOffset.MinValue;
                    match.SecondPlayer.MatchCount = 0;
                    match.SecondPlayer.FirstMatch = DateTimeOffset.MinValue;
                }

                currentMonth = currentMonth.AddMonths(1);
            }

            return View(new PlayerRatingHistoryViewModel
            {
                Player = player,
                LeagueId = id,
                MonthlyRatings = monthlyRatings,
                SwaOnly = swaOnly
            });
        }

    }
}