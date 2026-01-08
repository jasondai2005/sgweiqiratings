using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;


        /// <summary>
        /// Gets the last second of the month for the given date.
        /// E.g., Jan 15, 2024 → Jan 31, 2024 23:59:59
        /// </summary>
        private static DateTime GetEndOfMonth(DateTime date)
            => new DateTime(date.Year, date.Month, 1).AddMonths(1).AddSeconds(-1);

        /// <summary>
        /// Gets the last second of the month for the given date as DateTimeOffset.
        /// </summary>
        private static DateTimeOffset GetEndOfMonth(DateTimeOffset date)
            => new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, date.Offset).AddMonths(1).AddSeconds(-1);

        // Cache duration for league data (5 minutes)
        private static readonly TimeSpan LeagueCacheDuration = TimeSpan.FromMinutes(5);
        
        // Cache duration for rating calculations (2 minutes - shorter since ratings change)
        private static readonly TimeSpan RatingsCacheDuration = TimeSpan.FromMinutes(2);

        public LeaguesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            ILeaguesRepository leaguesRepository, IWebHostEnvironment env, IMemoryCache cache)
        {
            _context = context;
            _userManager = userManager;
            _leaguesRepository = leaguesRepository;
            _env = env;
            _cache = cache;
        }

        /// <summary>
        /// Gets league with all matches and players from cache, or loads from database if not cached.
        /// OPTIMIZED: Removed deep includes for Tournament.TournamentPlayers which caused exponential data loading.
        /// Rankings only need date, grade, and organization - not the full tournament data.
        /// </summary>
        private League GetLeagueWithMatches(Guid leagueId, bool forceRefresh = false)
        {
            string cacheKey = $"League_{leagueId}";

            if (forceRefresh)
            {
                _cache.Remove(cacheKey);
            }

            if (!_cache.TryGetValue(cacheKey, out League league))
            {
                league = _context.League
                    .Include(l => l.Matches).ThenInclude(m => m.FirstPlayer).ThenInclude(p => p.Rankings)
                    .Include(l => l.Matches).ThenInclude(m => m.SecondPlayer).ThenInclude(p => p.Rankings)
                    .Include(l => l.Matches).ThenInclude(m => m.Tournament)
                    .AsSplitQuery() // Use split queries for better performance with multiple includes
                    .SingleOrDefault(m => m.Id == leagueId);

                if (league != null)
                {
                    // Detach entities before caching so they're not tied to this DbContext
                    _context.ChangeTracker.Clear();
                    
                    _cache.Set(cacheKey, league, LeagueCacheDuration);
                }
            }

            return league;
        }

        /// <summary>
        /// Gets the SWA Only preference from cookie.
        /// </summary>
        private bool GetSwaOnlyPreference()
        {
            return Request.Cookies[HomeController.SwaOnlyCookieName] == "true";
        }
        
        /// <summary>
        /// Filters matches for a specific player based on date and match type.
        /// For non-SG leagues (international), all matches are included.
        /// </summary>
        private static IOrderedEnumerable<Match> FilterPlayerMatches(IEnumerable<Match> matches, string playerId, bool swaOnly, bool isSgLeague = true)
        {
            var playerFiltered = matches.Where(m => m.FirstPlayerId == playerId || m.SecondPlayerId == playerId);
            return RatingCalculationHelper.ApplySwaFilter(playerFiltered, swaOnly, isSgLeague);
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

            var swaOnly = Elo.SwaRankedPlayersOnly;
            
            var allPlayers = _context.LeaguePlayers
                .AsNoTracking()
                .Include(lp => lp.User).ThenInclude(u => u.Rankings)
                .Where(lp =>
                    lp.LeagueId == league.Id &&
                    lp.User.DisplayName != "Admin").ToList();
            if (swaOnly)
                allPlayers = allPlayers.Where(x => x.User.LatestSwaRanking.Any()).ToList();
            
            // For Singapore Weiqi league, separate local and non-local players
            bool isSgLeague = league.Name?.Contains("Singapore Weiqi") ?? false;
            
            // Set ViewData for navbar toggle visibility
            ViewData["IsSgLeague"] = isSgLeague;
            
            List<LeaguePlayer> players;
            List<LeaguePlayer> nonLocalPlayers = new List<LeaguePlayer>();
            
            if (isSgLeague)
            {
                players = allPlayers.Where(x => x.User.IsLocalPlayer || !string.IsNullOrEmpty(x.User.LatestSwaRanking)).ToList();
                nonLocalPlayers = allPlayers.Where(x => !x.User.IsLocalPlayer && string.IsNullOrEmpty(x.User.LatestSwaRanking)).ToList();
                nonLocalPlayers.Sort(CompareByRankingAndName);
            }
            else
            {
                players = allPlayers;
            }

            players.Sort(CompareByRankingAndName);

            // Check if current user is admin for this league
            var isAdmin = _leaguesRepository.GetAdminAuthorizedLeague(currentUser, id) != null;
            
            return View(new LeagueDetailsViewModel
            {
                League = league,
                Players = players,
                NonLocalPlayers = nonLocalPlayers,
                SwaRankedPlayersOnly = Elo.SwaRankedPlayersOnly,
                IsAdmin = isAdmin
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
            // Legacy support - redirect to SetPlayerStatus
            return await SetPlayerStatus(playerId, block ? PlayerStatus.Blocked : PlayerStatus.Normal);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPlayerStatus(Guid playerId, PlayerStatus status)
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

            player.Status = status;

            _context.SaveChanges();

            // Invalidate cache since player status changed
            _cache.Remove($"League_{league.Id}");

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
        private EloStat elo = new EloStat();
        public async Task<IActionResult> Rating(Guid? id, string byDate, bool refresh = false)
        {
            if (id == null)
            {
                return NotFound();
            }
            // When a date is specified, use end of day (23:59:59) to include all matches on that day
            var date = byDate == null 
                ? DateTimeOffset.UtcNow 
                : DateTimeOffset.ParseExact(byDate, "dd/MM/yyyy", null).AddDays(1).AddSeconds(-1);
            League.CutoffDate = date;
            var currentUser = await User.GetApplicationUser(_userManager);

            // First check if user is authorized for this league (lightweight check)
            if (!_context.LeaguePlayers.Any(lp => lp.LeagueId == id.Value && lp.UserId == currentUser.Id))
            {
                return NotFound();
            }

            // Load league with all required data (cached for performance)
            var league = GetLeagueWithMatches(id.Value, refresh);

            if (league == null)
            {
                return NotFound();
            }
            
            // Get SWA Only preference from cookie (only applies to SG league)
            bool isSgLeague = league.Name?.Contains("Singapore Weiqi") ?? false;
            bool swaOnly = isSgLeague && GetSwaOnlyPreference();
            
            // Set ViewData for navbar toggle visibility
            ViewData["IsSgLeague"] = isSgLeague;

            // Get player statuses for filtering
            var (notBlockedUserIds, hiddenUserIds, alwaysShownUserIds) = GetPlayerStatuses(league.Id);

            // Use shared calculation method - also collect matches for lastMatches display
            var winRateStat = new WinRateStat();
            var recentMatches = new List<Match>();
            var (eloResult, activeUsers, _) = CalculateRatings(
                league, date, swaOnly,
                allowedUserIds: notBlockedUserIds,
                onMatchProcessed: (match, _) => {
                    winRateStat.AddMatch(match);
                    // Only add non-bye matches (both players must exist) to recent matches
                    if (match.Date > date.AddMonths(-1) && match.FirstPlayer != null && match.SecondPlayer != null)
                        recentMatches.Add(match);
                });
            elo = eloResult;
            
            var stats = new List<IStat> { elo, winRateStat };

            // Filter users for display
            var users = activeUsers
                .Where(x => (!isSgLeague || !x.IsHiddenPlayer) && !hiddenUserIds.Contains(x.Id))
                .ToList();
            
            // Separate inactive users
            var inactiveUsers = users.Where(x => !x.Active && !x.IsProPlayer).ToList();
            
            // Add AlwaysShown users who may not have played any matches
            var activeUserIds = new HashSet<string>(activeUsers.Select(u => u.Id));
            var missingAlwaysShownIds = alwaysShownUserIds.Where(uid => !activeUserIds.Contains(uid)).ToList();
            if (missingAlwaysShownIds.Any())
            {
                var alwaysShownUsers = await _context.Users
                    .AsNoTracking()
                    .Include(u => u.Rankings)
                    .Where(u => missingAlwaysShownIds.Contains(u.Id))
                    .ToListAsync();
                inactiveUsers.AddRange(alwaysShownUsers);
            }
            
            inactiveUsers.Sort(CompareByRatingAndName);
            users = users.Where(x => x.Active || x.IsProPlayer).ToList();
            
            // For Singapore Weiqi league, separate local and non-local players based on cutoff date
            List<ApplicationUser> nonLocalUsers = new List<ApplicationUser>();
            if (isSgLeague)
            {
                nonLocalUsers = users.Where(x => !x.IsLocalPlayerAt(date)).ToList();
                nonLocalUsers.Sort(CompareByRatingAndName);
                users = users.Where(x => x.IsLocalPlayerAt(date)).ToList();
            }
            
            users.Sort(CompareByRatingAndName);

            var promotedPlayers = activeUsers
                .Where(x => x.Promotion.Contains('→'))
                .ToList();
            promotedPlayers.Sort(CompareByRankingRatingAndName);

            // Calculate comparison data (last day of previous month)
            var previousRatings = new Dictionary<string, double>();
            var previousPositions = new Dictionary<string, int>();
            DateTimeOffset? comparisonDate = null;
            
            // Compare to the last day of the previous month (end of day)
            // This aligns with how monthly ratings are displayed in the Player page
            var firstDayOfCurrentMonth = new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, date.Offset);
            comparisonDate = firstDayOfCurrentMonth.AddSeconds(-1); // Last second of previous month
            
            // Run calculation for comparison date (use cached version since no callback needed)
            var (prevEloStat, prevActiveUsers, _) = CalculateRatingsCached(
                league, comparisonDate.Value, swaOnly,
                allowedUserIds: notBlockedUserIds);
            
            // Build previous ratings dictionary
            foreach (var user in prevActiveUsers)
            {
                previousRatings[user.Id] = prevEloStat[user];
            }
            
            // Build previous positions (same filtering as current display)
            // Exclude pro players as they show "Pro" instead of position number
            var prevUsers = prevActiveUsers
                .Where(x => (!isSgLeague || !x.IsHiddenPlayer) && !hiddenUserIds.Contains(x.Id))
                .Where(x => x.Active && !x.IsProPlayer)
                .ToList();
            
            if (isSgLeague)
            {
                prevUsers = prevUsers.Where(x => x.IsLocalPlayerAt(comparisonDate.Value)).ToList();
            }
            
            // Sort by rating descending
            prevUsers.Sort((x, y) =>
            {
                double ratingX = prevEloStat[x];
                double ratingY = prevEloStat[y];
                int result = ratingY.CompareTo(ratingX);
                if (result == 0)
                {
                    var ranking1 = x.GetCombinedRankingBeforeDate(comparisonDate.Value);
                    var ranking2 = y.GetCombinedRankingBeforeDate(comparisonDate.Value);
                    if (ranking1 != ranking2)
                        return string.Compare(ranking1, ranking2, StringComparison.OrdinalIgnoreCase);
                    return string.Compare(x.DisplayName, y.DisplayName, StringComparison.OrdinalIgnoreCase);
                }
                return result;
            });
            
            for (int i = 0; i < prevUsers.Count; i++)
            {
                previousPositions[prevUsers[i].Id] = i + 1;
            }

            // Restore cutoff date for view rendering (Promotion property uses League.CutoffDate)
            League.CutoffDate = date;

            return View(new RatingViewModel(stats, users, promotedPlayers, recentMatches, id.Value, byDate, swaOnly, isSgLeague, nonLocalUsers, inactiveUsers, previousRatings, previousPositions, comparisonDate));
        }


        /// <summary>
        /// Loads player statuses for filtering (blocked, hidden, always shown).
        /// </summary>
        private (HashSet<string> notBlocked, HashSet<string> hidden, HashSet<string> alwaysShown) 
            GetPlayerStatuses(Guid leagueId)
        {
            var leaguePlayers = _context.LeaguePlayers.AsNoTracking()
                .Where(lp => lp.LeagueId == leagueId).ToList();
            
            return (
                new HashSet<string>(leaguePlayers.Where(lp => lp.Status != PlayerStatus.Blocked).Select(lp => lp.UserId)),
                new HashSet<string>(leaguePlayers.Where(lp => lp.Status == PlayerStatus.Hidden).Select(lp => lp.UserId)),
                new HashSet<string>(leaguePlayers.Where(lp => lp.Status == PlayerStatus.AlwaysShown).Select(lp => lp.UserId))
            );
        }

        /// <summary>
        /// Shared rating calculation used by both Rating and Player pages.
        /// Wraps RatingCalculationHelper.CalculateRatings and adds isSgLeague detection.
        /// </summary>
        /// <param name="league">League with matches loaded</param>
        /// <param name="cutoffDate">Date to calculate ratings up to</param>
        /// <param name="swaOnly">Filter for SWA tournaments only</param>
        /// <param name="allowedUserIds">Optional filter - only include these user IDs (null = include all)</param>
        /// <param name="onMatchProcessed">Optional callback for each match with EloStat (for monthly snapshots)</param>
        /// <returns>Tuple of (EloStat, activeUsers, isSgLeague)</returns>
        private static (EloStat eloStat, HashSet<ApplicationUser> activeUsers, bool isSgLeague) 
            CalculateRatings(
                League league, 
                DateTimeOffset cutoffDate, 
                bool swaOnly,
                HashSet<string> allowedUserIds = null,
                Action<Match, EloStat> onMatchProcessed = null)
        {
            bool isSgLeague = league.Name?.Contains("Singapore Weiqi") ?? false;
            
            var (eloStat, activeUsers) = RatingCalculationHelper.CalculateRatings(
                league.Matches,
                cutoffDate,
                swaOnly,
                isSgLeague,
                allowedUserIds,
                onMatchProcessed);

            return (eloStat, activeUsers, isSgLeague);
        }
        
        /// <summary>
        /// Cached version of rating calculation. Use when no match callback is needed.
        /// Caches results for 2 minutes to avoid redundant calculations.
        /// </summary>
        private (EloStat eloStat, HashSet<ApplicationUser> activeUsers, bool isSgLeague) 
            CalculateRatingsCached(
                League league, 
                DateTimeOffset cutoffDate, 
                bool swaOnly,
                HashSet<string> allowedUserIds = null)
        {
            // Create cache key from parameters (use end of month for cutoff to maximize cache hits)
            var endOfMonth = GetEndOfMonth(cutoffDate);
            string cacheKey = $"Ratings_{league.Id}_{endOfMonth:yyyyMMdd}_{swaOnly}";
            
            if (_cache.TryGetValue(cacheKey, out (EloStat eloStat, HashSet<ApplicationUser> activeUsers, bool isSgLeague) cached))
            {
                return cached;
            }
            
            var result = CalculateRatings(league, cutoffDate, swaOnly, allowedUserIds);
            _cache.Set(cacheKey, result, RatingsCacheDuration);
            return result;
        }

        private int CompareByRatingAndName(ApplicationUser x, ApplicationUser y)
        {
            if (!x.Active && y.Active)
                return 1;
            if (x.Active && !y.Active)
                return -1;

            var ranking1 = x.GetCombinedRankingBeforeDate(League.CutoffDate);
            var ranking2 = y.GetCombinedRankingBeforeDate(League.CutoffDate);
            return CompareRatings(x, y, ranking1, ranking2);
        }

        private int CompareByRankingRatingAndName(ApplicationUser x, ApplicationUser y)
        {
            var ranking1 = x.GetCombinedRankingBeforeDate(League.CutoffDate);
            var ranking2 = y.GetCombinedRankingBeforeDate(League.CutoffDate);
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
            var rating1 = elo.GetDoubleResult(x);
            var rating2 = elo.GetDoubleResult(y);
            
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
                if (rankingRating1 <= 1800) // will not differentiate kyus (10K and below) certified by SWA or other
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
        public async Task<IActionResult> Player(Guid id, string playerId, bool refresh = false)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return NotFound();
            }

            var currentUser = await User.GetApplicationUser(_userManager);

            // First check if user is authorized for this league (lightweight check)
            if (!_context.LeaguePlayers.Any(lp => lp.LeagueId == id && lp.UserId == currentUser.Id))
            {
                return NotFound();
            }

            // Load league with all required data (cached for performance)
            var league = GetLeagueWithMatches(id, refresh);

            if (league == null)
            {
                return NotFound();
            }

            // Get player statuses and league type info
            var (notBlockedUserIds, hiddenUserIds, _) = GetPlayerStatuses(league.Id);
            bool isSgLeague = league.Name?.Contains("Singapore Weiqi") ?? false;
            
            // Get SWA Only preference from cookie (only applies to SG league)
            bool swaOnly = isSgLeague && GetSwaOnlyPreference();
            
            // Set ViewData for navbar toggle visibility
            ViewData["IsSgLeague"] = isSgLeague;

            // Get player from already loaded match data (includes Rankings via ThenInclude)
            var player = league.Matches
                .Where(m => m.FirstPlayerId == playerId)
                .Select(m => m.FirstPlayer)
                .FirstOrDefault()
                ?? league.Matches
                .Where(m => m.SecondPlayerId == playerId)
                .Select(m => m.SecondPlayer)
                .FirstOrDefault();
            
            // If player not found in matches, load directly from database (they may have no matches yet)
            if (player == null)
            {
                // Only include Rankings - don't need Tournament.TournamentPlayers for player page
                player = await _context.Users
                    .Include(u => u.Rankings)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == playerId);
                
                if (player == null)
                {
                    return NotFound();
                }
            }

            // Find player's matches (all matches for intl leagues, filtered for local leagues)
            var playerMatches = FilterPlayerMatches(league.Matches, playerId, swaOnly, isSgLeague).ToList();

            // Load player's tournament positions for display in rating history
            var playerTournamentPositions = await _context.TournamentPlayers
                .Where(tp => tp.PlayerId == playerId)
                .ToDictionaryAsync(tp => tp.TournamentId, tp => new { tp.Position, tp.FemalePosition, tp.TeamPosition });

            // If player has no matches, show page with just their info (no rating history)
            if (!playerMatches.Any())
            {
                // Load tournaments for ranking dropdown
                var emptyTournamentOptions = await _context.Tournaments
                    .Where(t => t.LeagueId == league.Id)
                    .OrderByDescending(t => t.StartDate)
                    .Select(t => new ViewModels.Player.TournamentOption
                    {
                        Id = t.Id,
                        Name = t.FullName
                    })
                    .ToListAsync();
                
                // Load tournament participations even for players with no matches
                var emptyTournamentParticipations = await _context.TournamentPlayers
                    .Where(tp => tp.PlayerId == playerId && tp.Tournament.StartDate.HasValue)
                    .Include(tp => tp.Tournament)
                    .Select(tp => new ViewModels.Player.TournamentParticipation
                    {
                        TournamentId = tp.TournamentId,
                        TournamentName = tp.Tournament.FullName,
                        StartDate = tp.Tournament.StartDate.Value,
                        Position = tp.Position,
                        FemalePosition = tp.FemalePosition,
                        TeamPosition = tp.TeamPosition,
                        HasMatches = false,
                        TournamentFactor = tp.Tournament.Factor,
                        Organizer = tp.Tournament.Organizer,
                        TournamentType = tp.Tournament.TournamentType,
                        IsIntlSelection = tp.Tournament.TournamentType == Tournament.TypeIntlSelection,
                        IsTitle = tp.Tournament.TournamentType == Tournament.TypeTitle,
                        TitleEn = tp.Tournament.TournamentType == Tournament.TypeTitle ? tp.Tournament.TitleEn : null,
                        TitleCn = tp.Tournament.TournamentType == Tournament.TypeTitle ? tp.Tournament.TitleCn : null
                    })
                    .ToListAsync();
                
                // Build title lists for empty player
                var emptyOneYearAgo = DateTimeOffset.UtcNow.AddYears(-1);
                var emptyActiveTitles = emptyTournamentParticipations
                    .Where(tp => tp.IsTitle && tp.Position == 1 && !string.IsNullOrEmpty(tp.TitleEn) && tp.StartDate > emptyOneYearAgo)
                    .OrderByDescending(tp => tp.StartDate)
                    .Select(tp => new ViewModels.Player.TitleInfo
                    {
                        TitleEn = tp.TitleEn,
                        TitleCn = tp.TitleCn,
                        WonDate = tp.StartDate,
                        TournamentName = tp.TournamentName
                    })
                    .ToList();
                
                var emptyFormerTitles = emptyTournamentParticipations
                    .Where(tp => tp.IsTitle && tp.Position == 1 && !string.IsNullOrEmpty(tp.TitleEn) && tp.StartDate <= emptyOneYearAgo)
                    .OrderByDescending(tp => tp.StartDate)
                    .Select(tp => new ViewModels.Player.TitleInfo
                    {
                        TitleEn = tp.TitleEn,
                        TitleCn = tp.TitleCn,
                        WonDate = tp.StartDate,
                        TournamentName = tp.TournamentName
                    })
                    .ToList();
                    
                return View(new PlayerRatingHistoryViewModel
                {
                    LeagueId = league.Id,
                    Player = player,
                    MonthlyRatings = new List<MonthlyRating>(),
                    SwaOnly = swaOnly,
                    GameRecords = new List<GameRecord>(),
                    TournamentOptions = emptyTournamentOptions,
                    TournamentParticipations = emptyTournamentParticipations,
                    ChampionshipCount = emptyTournamentParticipations.Count(tp => tp.Position == 1),
                    TeamChampionshipCount = emptyTournamentParticipations.Count(tp => tp.TeamPosition == 1),
                    FemaleChampionshipCount = emptyTournamentParticipations.Count(tp => tp.FemalePosition == 1),
                    ActiveTitles = emptyActiveTitles,
                    FormerTitles = emptyFormerTitles,
                    IntlSelectionCount = emptyTournamentParticipations.Count(tp => tp.IsIntlSelection)
                });
            }

            var firstMatchDate = playerMatches.First().Date;
            if (player.FirstMatch == DateTimeOffset.MinValue)
                player.FirstMatch = firstMatchDate;

            // For new players with foreign/unknown ranking, start from when rating is corrected (after 12 games)
            bool isNewForeignPlayer = player.IsUnknownRankedPlayer;
            
            DateTimeOffset startDate = firstMatchDate;
            if (isNewForeignPlayer && playerMatches.Count >= 12)
            {
                startDate = playerMatches[11].Date; // 12th game (0-indexed)
            }
            
            // ===== Use shared calculation with monthly snapshot callback =====
            var monthlyRatings = new List<MonthlyRating>();
            var startMonth = GetEndOfMonth(startDate.DateTime);
            var currentProcessingMonth = new DateTime(1900, 1, 1);
            int playerMatchCount = 0;
            int matchesInCurrentMonth = 0;
            var matchInfosInCurrentMonth = new List<MatchInfo>();
            EloStat currentEloStat = null;
            
            // Track all players' last match dates for position calculation
            var playerLastMatchDates = new Dictionary<string, DateTimeOffset>();
            var allPlayersInMatches = new Dictionary<string, ApplicationUser>();
            
            // Callback to capture monthly snapshots during processing
            void OnMatchProcessed(Match match, EloStat elo)
            {
                currentEloStat = elo;
                var matchMonth = GetEndOfMonth(match.Date.DateTime);
                
                // Track all players for position calculation (skip NULL players for bye matches)
                if (!string.IsNullOrEmpty(match.FirstPlayerId) && !allPlayersInMatches.ContainsKey(match.FirstPlayerId))
                    allPlayersInMatches[match.FirstPlayerId] = match.FirstPlayer;
                if (!string.IsNullOrEmpty(match.SecondPlayerId) && !allPlayersInMatches.ContainsKey(match.SecondPlayerId))
                    allPlayersInMatches[match.SecondPlayerId] = match.SecondPlayer;
                if (!string.IsNullOrEmpty(match.FirstPlayerId))
                    playerLastMatchDates[match.FirstPlayerId] = match.Date;
                if (!string.IsNullOrEmpty(match.SecondPlayerId))
                    playerLastMatchDates[match.SecondPlayerId] = match.Date;
                
                // When month changes, capture snapshot of previous month
                if (matchMonth > currentProcessingMonth && currentProcessingMonth.Year > 1900)
                {
                    CaptureSnapshot(currentProcessingMonth);
                    
                    // Clear match data before filling skipped months (they have no matches)
                    matchesInCurrentMonth = 0;
                    matchInfosInCurrentMonth.Clear();
                    
                    // Fill in any skipped months (with empty match names)
                    // Use GetEndOfMonth to ensure consistent end-of-month DateTime values
                    var nextMonth = GetEndOfMonth(currentProcessingMonth.AddMonths(1));
                    while (nextMonth < matchMonth)
                    {
                        CaptureSnapshot(nextMonth);
                        nextMonth = GetEndOfMonth(nextMonth.AddMonths(1));
                    }
                }
                currentProcessingMonth = matchMonth;
                
                // Track this player's monthly stats
                if (match.FirstPlayerId == playerId || match.SecondPlayerId == playerId)
                {
                    playerMatchCount++;
                    matchesInCurrentMonth++;
                    // Track unique match info (by tournament or match name)
                    var matchKey = match.TournamentId?.ToString() ?? match.MatchName ?? "";
                    if (!matchInfosInCurrentMonth.Any(m => (m.TournamentId?.ToString() ?? m.MatchName ?? "") == matchKey))
                    {
                        int? tournamentPosition = null;
                        int? femalePosition = null;
                        int? teamPosition = null;
                        if (match.TournamentId.HasValue && playerTournamentPositions.TryGetValue(match.TournamentId.Value, out var posInfo))
                        {
                            tournamentPosition = posInfo.Position;
                            femalePosition = posInfo.FemalePosition;
                            teamPosition = posInfo.TeamPosition;
                        }
                        matchInfosInCurrentMonth.Add(new MatchInfo
                        {
                            MatchName = match.MatchName,
                            TournamentId = match.TournamentId,
                            TournamentName = match.Tournament?.FullName,
                            Round = match.Round,
                            TournamentPosition = tournamentPosition,
                            FemalePosition = femalePosition,
                            TeamPosition = teamPosition,
                            TournamentStartDate = match.Tournament?.StartDate
                        });
                    }
                }
            }
            
            void CaptureSnapshot(DateTime monthToCapture)
            {
                if (monthToCapture < startMonth || currentEloStat == null)
                    return;
                    
                bool hasEnoughGames = !isNewForeignPlayer || playerMatchCount >= 12;
                if (playerMatchCount > 0 && hasEnoughGames)
                {
                    // Order tournaments by start date descending (latest at top)
                    var orderedMatches = matchInfosInCurrentMonth
                        .OrderByDescending(m => m.TournamentStartDate ?? DateTimeOffset.MinValue)
                        .ToList();
                    
                    // monthToCapture is already end-of-month, just convert to DateTimeOffset
                    var monthEnd = new DateTimeOffset(monthToCapture, TimeSpan.Zero);
                    
                    // For the current month, use 'now' instead of month-end to reflect current state
                    // This avoids confusion (e.g., showing position based on future end-of-month)
                    var now = DateTimeOffset.UtcNow;
                    var isCurrentMonth = monthToCapture.Year == now.Year && monthToCapture.Month == now.Month;
                    var effectiveDate = isCurrentMonth ? now : monthEnd;
                    
                    // Apply promotions for all tracked players up to this month's end
                    // This ensures players who got promoted but had no match get their bonus
                    currentEloStat.ApplyPromotionsUpToDate(allPlayersInMatches.Values, effectiveDate, isSgLeague);
                    var twoYearsAgo = effectiveDate.AddYears(-2);
                    
                    // Filter to active, rankable players at this month
                    var rankableAtMonth = allPlayersInMatches.Values
                        .Where(u => notBlockedUserIds.Contains(u.Id)
                            && playerLastMatchDates.TryGetValue(u.Id, out var lastMatch) && lastMatch > twoYearsAgo
                            && !u.IsProPlayer
                            && (!isSgLeague || !u.IsHiddenPlayer)
                            && (hiddenUserIds == null || !hiddenUserIds.Contains(u.Id))
                            && (!isSgLeague || u.IsLocalPlayerAt(effectiveDate)))
                        .ToList();
                    
                    // Sort by rating (descending), then ranking, then name
                    rankableAtMonth.Sort((x, y) =>
                    {
                        double ratingX = currentEloStat[x];
                        double ratingY = currentEloStat[y];
                        int result = ratingY.CompareTo(ratingX);
                        if (result == 0)
                        {
                            var ranking1 = x.GetCombinedRankingBeforeDate(effectiveDate);
                            var ranking2 = y.GetCombinedRankingBeforeDate(effectiveDate);
                            if (ranking1 != ranking2)
                                return string.Compare(ranking1, ranking2, StringComparison.OrdinalIgnoreCase);
                            return string.Compare(x.DisplayName, y.DisplayName, StringComparison.OrdinalIgnoreCase);
                        }
                        return result;
                    });
                    
                    int monthPosition = rankableAtMonth.FindIndex(u => u.Id == playerId) + 1;
                    int monthTotalPlayers = rankableAtMonth.Count;
                    
                    // Get any promotion bonuses applied this month for this player
                    var promotionBonuses = currentEloStat.ConsumePromotionBonuses(playerId, monthEnd);
                    
                    // Filter out promotion bonuses that happened before the player's first match
                    // (no bonus should be shown for pre-match promotions)
                    var playerFirstMatch = player.FirstMatch;
                    if (playerFirstMatch != DateTimeOffset.MinValue)
                    {
                        promotionBonuses = promotionBonuses
                            .Where(b => !b.promotionDate.HasValue || b.promotionDate.Value >= playerFirstMatch)
                            .ToList();
                    }
                    
                    monthlyRatings.Add(new MonthlyRating
                    {
                        Month = monthToCapture,
                        Rating = currentEloStat[player],
                        MatchesInMonth = matchesInCurrentMonth,
                        Matches = orderedMatches,
                        Position = monthPosition,
                        TotalPlayers = monthTotalPlayers,
                        PromotionBonuses = promotionBonuses
                    });
                }
            }
            
            // Use shared calculation (same as Rating page)
            var (eloStat, activeUsers, _) = CalculateRatings(
                league, 
                DateTimeOffset.UtcNow, 
                swaOnly,
                allowedUserIds: notBlockedUserIds,
                onMatchProcessed: OnMatchProcessed);
            currentEloStat = eloStat;
            
            // Capture final month and fill remaining months
            if (currentProcessingMonth.Year > 1900)
            {
                CaptureSnapshot(currentProcessingMonth);
                
                var endMonth = GetEndOfMonth(DateTime.Now);
                // Use GetEndOfMonth to ensure consistent end-of-month DateTime values
                var nextMonth = GetEndOfMonth(currentProcessingMonth.AddMonths(1));
                while (nextMonth <= endMonth)
                {
                    matchesInCurrentMonth = 0;
                    matchInfosInCurrentMonth.Clear();
                    CaptureSnapshot(nextMonth);
                    nextMonth = GetEndOfMonth(nextMonth.AddMonths(1));
                }
            }
            
            // Use the latest month's position from monthlyRatings to ensure consistency
            // This guarantees Position field matches the latest entry in Monthly Rating History
            var latestMonthlyRating = monthlyRatings.OrderByDescending(r => r.Month).FirstOrDefault();
            int position = latestMonthlyRating?.Position ?? 0;
            int totalPlayers = latestMonthlyRating?.TotalPlayers ?? 0;
            
            // Build ranked players list for navigation (previous/next player)
            var now = DateTimeOffset.UtcNow;
            var twoYearsAgoNav = now.AddYears(-2);
            var rankedPlayerIds = new HashSet<string>();
            var rankedPlayersForNav = allPlayersInMatches.Values
                .Where(u => notBlockedUserIds.Contains(u.Id)
                    && playerLastMatchDates.TryGetValue(u.Id, out var lastMatch) && lastMatch > twoYearsAgoNav
                    && !u.IsProPlayer
                    && (!isSgLeague || !u.IsHiddenPlayer)
                    && (hiddenUserIds == null || !hiddenUserIds.Contains(u.Id))
                    && (!isSgLeague || u.IsLocalPlayerAt(now)))
                .ToList();
            
            rankedPlayersForNav.Sort((x, y) =>
            {
                double ratingX = eloStat[x];
                double ratingY = eloStat[y];
                int result = ratingY.CompareTo(ratingX);
                if (result == 0)
                {
                    var ranking1 = x.GetCombinedRankingBeforeDate(now);
                    var ranking2 = y.GetCombinedRankingBeforeDate(now);
                    if (ranking1 != ranking2)
                        return string.Compare(ranking1, ranking2, StringComparison.OrdinalIgnoreCase);
                    return string.Compare(x.DisplayName, y.DisplayName, StringComparison.OrdinalIgnoreCase);
                }
                return result;
            });
            
            foreach (var u in rankedPlayersForNav)
                rankedPlayerIds.Add(u.Id);
            
            // Get unranked players (inactive, overseas, etc.) for continued navigation
            var unrankedPlayersForNav = allPlayersInMatches.Values
                .Where(u => notBlockedUserIds.Contains(u.Id)
                    && !rankedPlayerIds.Contains(u.Id)
                    && !u.IsProPlayer
                    && (!isSgLeague || !u.IsHiddenPlayer)
                    && (hiddenUserIds == null || !hiddenUserIds.Contains(u.Id)))
                .OrderByDescending(u => eloStat[u]) // Sort by rating descending
                .ThenBy(u => u.DisplayName)
                .ToList();
            
            var unrankedPlayerIds = new HashSet<string>(unrankedPlayersForNav.Select(u => u.Id));
            
            // Get hidden players for navigation after unranked
            var hiddenPlayersForNav = allPlayersInMatches.Values
                .Where(u => notBlockedUserIds.Contains(u.Id)
                    && !rankedPlayerIds.Contains(u.Id)
                    && !unrankedPlayerIds.Contains(u.Id)
                    && !u.IsProPlayer
                    && (u.IsHiddenPlayer || (hiddenUserIds != null && hiddenUserIds.Contains(u.Id))))
                .OrderByDescending(u => eloStat[u]) // Sort by rating descending
                .ThenBy(u => u.DisplayName)
                .ToList();
            
            // Combine: ranked players first, then unranked, then hidden
            var allPlayersForNav = rankedPlayersForNav.Concat(unrankedPlayersForNav).Concat(hiddenPlayersForNav).ToList();
            
            // Find previous and next player IDs based on current position in combined list
            int currentIndex = allPlayersForNav.FindIndex(u => u.Id == playerId);
            string previousPlayerId = currentIndex > 0 ? allPlayersForNav[currentIndex - 1].Id : null;
            string nextPlayerId = currentIndex >= 0 && currentIndex < allPlayersForNav.Count - 1 
                ? allPlayersForNav[currentIndex + 1].Id : null;

            // Build game records from player matches
            var gameRecords = new List<GameRecord>();
            
            foreach (var match in playerMatches)
            {
                bool isFirstPlayer = match.FirstPlayerId == playerId;
                var opponent = isFirstPlayer ? match.SecondPlayer : match.FirstPlayer;
                var playerScore = isFirstPlayer ? match.FirstPlayerScore : match.SecondPlayerScore;
                var opponentScore = isFirstPlayer ? match.SecondPlayerScore : match.FirstPlayerScore;
                
                string result;
                if (playerScore > opponentScore)
                    result = "Win";
                else if (playerScore < opponentScore)
                    result = "Loss";
                else
                    result = "Draw";
                
                // Get opponent ranking at time of match (handle BYE matches where opponent is null)
                // Use just the date (midnight) to exclude same-day promotions
                // since promotions happen at the end of the day after all matches
                var opponentRanking = opponent?.GetCombinedRankingBeforeDate(match.Date.Date);
                
                int? tournamentPosition = null;
                int? femalePosition = null;
                int? teamPosition = null;
                if (match.TournamentId.HasValue && playerTournamentPositions.TryGetValue(match.TournamentId.Value, out var posInfo))
                {
                    tournamentPosition = posInfo.Position;
                    femalePosition = posInfo.FemalePosition;
                    teamPosition = posInfo.TeamPosition;
                }
                gameRecords.Add(new GameRecord
                {
                    Date = match.Date,
                    MatchName = match.MatchName,
                    OpponentName = opponent?.DisplayName ?? "BYE",
                    OpponentRanking = opponentRanking,
                    OpponentId = opponent?.Id,
                    Result = result,
                    Factor = match.Factor,
                    TournamentId = match.TournamentId,
                    TournamentName = match.Tournament?.FullName,
                    Round = match.Round,
                    TournamentPosition = tournamentPosition,
                    FemalePosition = femalePosition,
                    TeamPosition = teamPosition
                });
            }

            // Load tournaments for ranking dropdown
            var tournamentOptions = await _context.Tournaments
                .Where(t => t.LeagueId == id)
                .OrderByDescending(t => t.StartDate)
                .Select(t => new ViewModels.Player.TournamentOption
                {
                    Id = t.Id,
                    Name = t.FullName
                })
                .ToListAsync();

            // Load all tournament participations (including those without match records)
            // Use tournament IDs from any match (regardless of factor) to determine if matches exist
            var tournamentIdsWithMatches = new HashSet<Guid>(
                gameRecords.Where(g => g.TournamentId.HasValue).Select(g => g.TournamentId.Value));
            
            var tournamentParticipations = await _context.TournamentPlayers
                .Where(tp => tp.PlayerId == playerId && tp.Tournament.StartDate.HasValue)
                .Include(tp => tp.Tournament)
                .Select(tp => new ViewModels.Player.TournamentParticipation
                {
                    TournamentId = tp.TournamentId,
                    TournamentName = tp.Tournament.FullName,
                    StartDate = tp.Tournament.StartDate.Value,
                    Position = tp.Position,
                    FemalePosition = tp.FemalePosition,
                    TeamPosition = tp.TeamPosition,
                    HasMatches = false, // Will be set after query
                    TournamentFactor = tp.Tournament.Factor, // Tournament's own factor (determines if rated)
                    Organizer = tp.Tournament.Organizer,
                    TournamentType = tp.Tournament.TournamentType,
                    IsIntlSelection = tp.Tournament.TournamentType == Tournament.TypeIntlSelection,
                    IsTitle = tp.Tournament.TournamentType == Tournament.TypeTitle,
                    TitleEn = tp.Tournament.TournamentType == Tournament.TypeTitle ? tp.Tournament.TitleEn : null,
                    TitleCn = tp.Tournament.TournamentType == Tournament.TypeTitle ? tp.Tournament.TitleCn : null
                })
                .ToListAsync();
            
            // Mark which tournaments have matches (regardless of factor)
            foreach (var tp in tournamentParticipations)
            {
                tp.HasMatches = tournamentIdsWithMatches.Contains(tp.TournamentId);
            }
            
            // Build title lists
            var oneYearAgo = DateTimeOffset.UtcNow.AddYears(-1);
            var activeTitles = tournamentParticipations
                .Where(tp => tp.IsTitle && tp.Position == 1 && !string.IsNullOrEmpty(tp.TitleEn) && tp.StartDate > oneYearAgo)
                .OrderByDescending(tp => tp.StartDate)
                .Select(tp => new ViewModels.Player.TitleInfo
                {
                    TitleEn = tp.TitleEn,
                    TitleCn = tp.TitleCn,
                    WonDate = tp.StartDate,
                    TournamentName = tp.TournamentName
                })
                .ToList();
            
            var formerTitles = tournamentParticipations
                .Where(tp => tp.IsTitle && tp.Position == 1 && !string.IsNullOrEmpty(tp.TitleEn) && tp.StartDate <= oneYearAgo)
                .OrderByDescending(tp => tp.StartDate)
                .Select(tp => new ViewModels.Player.TitleInfo
                {
                    TitleEn = tp.TitleEn,
                    TitleCn = tp.TitleCn,
                    WonDate = tp.StartDate,
                    TournamentName = tp.TournamentName
                })
                .ToList();
            
            // Count international selections
            var intlSelectionCount = tournamentParticipations.Count(tp => tp.IsIntlSelection);

            // Count championships (tournaments where player has Position = 1)
            var championshipCount = tournamentParticipations.Count(tp => tp.Position == 1);
            
            // Count team championships (tournaments where player has TeamPosition = 1)
            var teamChampionshipCount = tournamentParticipations.Count(tp => tp.TeamPosition == 1);
            
            // Count female championships (tournaments where player has FemalePosition = 1)
            var femaleChampionshipCount = tournamentParticipations.Count(tp => tp.FemalePosition == 1);

            return View(new PlayerRatingHistoryViewModel
            {
                Player = player,
                LeagueId = id,
                MonthlyRatings = monthlyRatings,
                GameRecords = gameRecords,
                SwaOnly = swaOnly,
                IsSgLeague = isSgLeague,
                Position = position,
                TotalPlayers = totalPlayers,
                PreviousPlayerId = previousPlayerId,
                NextPlayerId = nextPlayerId,
                TournamentOptions = tournamentOptions,
                TournamentParticipations = tournamentParticipations,
                ChampionshipCount = championshipCount,
                TeamChampionshipCount = teamChampionshipCount,
                FemaleChampionshipCount = femaleChampionshipCount,
                ActiveTitles = activeTitles,
                FormerTitles = formerTitles,
                IntlSelectionCount = intlSelectionCount
            });
        }

        // POST: Leagues/UpdatePlayerInfo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePlayerInfo(EditPlayerInfoViewModel model)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            
            if (!ModelState.IsValid)
            {
                if (isAjax) return Json(new { success = false, message = "Invalid data" });
                return RedirectToAction(nameof(Player), new { id = model.LeagueId, playerId = model.PlayerId });
            }

            var player = await _userManager.FindByIdAsync(model.PlayerId);
            if (player == null)
            {
                if (isAjax) return Json(new { success = false, message = "Player not found" });
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(model.DisplayName))
                player.DisplayName = model.DisplayName;
            player.BirthYearValue = model.BirthYearValue;
            player.Residence = model.Residence;
            player.Photo = model.Photo;

            await _userManager.UpdateAsync(player);

            if (isAjax) return Json(new { success = true, message = "Player info saved" });
            return RedirectToAction(nameof(Player), new { id = model.LeagueId, playerId = model.PlayerId });
        }

        // POST: Leagues/UploadPlayerPhoto
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadPlayerPhoto(string playerId, Guid leagueId, IFormFile photoFile)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            
            if (photoFile == null || photoFile.Length == 0)
            {
                if (isAjax) return Json(new { success = false, message = "No file selected" });
                return RedirectToAction(nameof(Player), new { id = leagueId, playerId = playerId });
            }

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(photoFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                if (isAjax) return Json(new { success = false, message = "Invalid file type. Supported: JPG, PNG, GIF, WebP" });
                return RedirectToAction(nameof(Player), new { id = leagueId, playerId = playerId });
            }

            // Validate file size (max 5MB)
            if (photoFile.Length > 5 * 1024 * 1024)
            {
                if (isAjax) return Json(new { success = false, message = "File too large. Maximum size: 5MB" });
                return RedirectToAction(nameof(Player), new { id = leagueId, playerId = playerId });
            }

            var player = await _userManager.FindByIdAsync(playerId);
            if (player == null)
            {
                if (isAjax) return Json(new { success = false, message = "Player not found" });
                return NotFound();
            }

            // Generate unique filename using player ID
            var fileName = $"{playerId}{extension}";
            var picFolder = Path.Combine(_env.WebRootPath, "pic");
            
            // Ensure the pic folder exists
            if (!Directory.Exists(picFolder))
            {
                Directory.CreateDirectory(picFolder);
            }

            var filePath = Path.Combine(picFolder, fileName);

            // Delete existing photo if exists (different extension)
            foreach (var ext in allowedExtensions)
            {
                var existingFile = Path.Combine(picFolder, $"{playerId}{ext}");
                if (System.IO.File.Exists(existingFile) && existingFile != filePath)
                {
                    System.IO.File.Delete(existingFile);
                }
            }

            // Save the file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await photoFile.CopyToAsync(stream);
            }

            // Update player's photo URL
            player.Photo = $"/pic/{fileName}";
            await _userManager.UpdateAsync(player);

            if (isAjax) return Json(new { success = true, message = "Photo uploaded", photoUrl = player.Photo });
            return RedirectToAction(nameof(Player), new { id = leagueId, playerId = playerId });
        }

        // GET: Leagues/EditRankingHistory - Separate page for editing player ranking history
        public async Task<IActionResult> EditRankingHistory(Guid id, string playerId, Guid? tournamentId = null)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return NotFound();
            }

            var currentUser = await User.GetApplicationUser(_userManager);

            // Check if user is authorized for this league
            if (!_context.LeaguePlayers.Any(lp => lp.LeagueId == id && lp.UserId == currentUser.Id))
            {
                return NotFound();
            }

            var league = await _context.League.FindAsync(id);
            if (league == null)
            {
                return NotFound();
            }

            var player = await _userManager.FindByIdAsync(playerId);
            if (player == null)
            {
                return NotFound();
            }

            // Load rankings for this player
            var rankings = await _context.PlayerRankings
                .Include(r => r.Tournament)
                .Where(r => r.PlayerId == playerId)
                .OrderByDescending(r => r.RankingDate ?? DateTimeOffset.MinValue)
                .ToListAsync();

            // Load tournaments for ranking dropdown
            var tournamentOptions = await _context.Tournaments
                .Where(t => t.LeagueId == id)
                .OrderByDescending(t => t.StartDate)
                .Select(t => new ViewModels.Player.TournamentOption
                {
                    Id = t.Id,
                    Name = t.FullName
                })
                .ToListAsync();

            return View(new ViewModels.Player.EditRankingHistoryViewModel
            {
                Player = player,
                LeagueId = id,
                LeagueName = league.Name,
                Rankings = rankings,
                TournamentOptions = tournamentOptions,
                PreselectedTournamentId = tournamentId
            });
        }

        // POST: Leagues/AddRanking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRanking(EditRankingViewModel model)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            
            if (!ModelState.IsValid)
            {
                if (isAjax) return Json(new { success = false, message = "Invalid data" });
                return RedirectToAction(nameof(Player), new { id = model.LeagueId, playerId = model.PlayerId });
            }

            var player = await _userManager.FindByIdAsync(model.PlayerId);
            if (player == null)
            {
                if (isAjax) return Json(new { success = false, message = "Player not found" });
                return NotFound();
            }

            var ranking = new PlayerRanking
            {
                RankingId = Guid.NewGuid(),
                PlayerId = model.PlayerId,
                Ranking = model.Ranking.ToUpper(),
                Organization = model.Organization == "Other" ? null : model.Organization,
                RankingDate = model.RankingDate.HasValue 
                    ? new DateTimeOffset(model.RankingDate.Value, TimeSpan.Zero) 
                    : null,
                RankingNote = model.RankingNote,
                TournamentId = model.TournamentId
            };

            _context.PlayerRankings.Add(ranking);

            // If tournament is selected, also set this ranking as the player's promotion in the tournament
            if (model.TournamentId.HasValue)
            {
                var tournamentPlayer = await _context.TournamentPlayers
                    .FirstOrDefaultAsync(tp => tp.TournamentId == model.TournamentId.Value && tp.PlayerId == model.PlayerId);
                if (tournamentPlayer != null)
                {
                    tournamentPlayer.PromotionId = ranking.RankingId;
                }
            }

            await _context.SaveChangesAsync();

            if (isAjax) return Json(new { 
                success = true, 
                message = "Ranking added",
                ranking = new {
                    rankingId = ranking.RankingId,
                    rankingGrade = ranking.Ranking,
                    organization = ranking.Organization,
                    date = ranking.RankingDate?.ToString("dd/MM/yyyy") ?? "-",
                    dateValue = ranking.RankingDate?.ToString("yyyy-MM-dd") ?? "",
                    note = ranking.RankingNote ?? ""
                }
            });
            return RedirectToAction(nameof(Player), new { id = model.LeagueId, playerId = model.PlayerId });
        }

        // POST: Leagues/EditRanking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRanking(EditRankingViewModel model)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            
            if (!ModelState.IsValid || !model.RankingId.HasValue)
            {
                if (isAjax) return Json(new { success = false, message = "Invalid data" });
                return RedirectToAction(nameof(Player), new { id = model.LeagueId, playerId = model.PlayerId });
            }

            var ranking = await _context.PlayerRankings.FindAsync(model.RankingId.Value);
            if (ranking == null || ranking.PlayerId != model.PlayerId)
            {
                if (isAjax) return Json(new { success = false, message = "Ranking not found" });
                return NotFound();
            }

            var oldTournamentId = ranking.TournamentId;
            
            ranking.Ranking = model.Ranking.ToUpper();
            ranking.Organization = model.Organization == "Other" ? null : model.Organization;
            ranking.RankingDate = model.RankingDate.HasValue 
                ? new DateTimeOffset(model.RankingDate.Value, TimeSpan.Zero) 
                : null;
            ranking.RankingNote = model.RankingNote;
            ranking.TournamentId = model.TournamentId;

            _context.PlayerRankings.Update(ranking);

            // Update TournamentPlayer.PromotionId when tournament changes
            if (oldTournamentId != model.TournamentId)
            {
                // Clear old tournament's promotion reference
                if (oldTournamentId.HasValue)
                {
                    var oldTournamentPlayer = await _context.TournamentPlayers
                        .FirstOrDefaultAsync(tp => tp.TournamentId == oldTournamentId.Value && tp.PlayerId == model.PlayerId && tp.PromotionId == ranking.RankingId);
                    if (oldTournamentPlayer != null)
                    {
                        oldTournamentPlayer.PromotionId = null;
                    }
                }

                // Set new tournament's promotion reference
                if (model.TournamentId.HasValue)
                {
                    var newTournamentPlayer = await _context.TournamentPlayers
                        .FirstOrDefaultAsync(tp => tp.TournamentId == model.TournamentId.Value && tp.PlayerId == model.PlayerId);
                    if (newTournamentPlayer != null)
                    {
                        newTournamentPlayer.PromotionId = ranking.RankingId;
                    }
                }
            }

            await _context.SaveChangesAsync();

            if (isAjax) return Json(new { 
                success = true, 
                message = "Ranking updated",
                ranking = new {
                    rankingId = ranking.RankingId,
                    rankingGrade = ranking.Ranking,
                    organization = ranking.Organization,
                    date = ranking.RankingDate?.ToString("dd/MM/yyyy") ?? "-",
                    dateValue = ranking.RankingDate?.ToString("yyyy-MM-dd") ?? "",
                    note = ranking.RankingNote ?? ""
                }
            });
            return RedirectToAction(nameof(Player), new { id = model.LeagueId, playerId = model.PlayerId });
        }

        // POST: Leagues/DeleteRanking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRanking(Guid rankingId, string playerId, Guid leagueId)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            
            var ranking = await _context.PlayerRankings.FindAsync(rankingId);
            if (ranking == null || ranking.PlayerId != playerId)
            {
                if (isAjax) return Json(new { success = false, message = "Ranking not found" });
                return NotFound();
            }

            _context.PlayerRankings.Remove(ranking);
            await _context.SaveChangesAsync();

            if (isAjax) return Json(new { success = true, message = "Ranking deleted", rankingId = rankingId });
            return RedirectToAction(nameof(Player), new { id = leagueId, playerId = playerId });
        }

        // POST: Leagues/SetPlayerStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPlayerStatus(Guid playerId, int status)
        {
            var leaguePlayer = await _context.LeaguePlayers.FindAsync(playerId);
            if (leaguePlayer == null)
            {
                return NotFound();
            }

            leaguePlayer.Status = (PlayerStatus)status;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = leaguePlayer.LeagueId });
        }

        // GET: Leagues/DeletePlayer - Confirmation page
        public async Task<IActionResult> DeletePlayer(Guid leagueId, string playerId)
        {
            var currentUser = await User.GetApplicationUser(_userManager);
            
            // Check if user is admin for this league
            var league = _leaguesRepository.GetAdminAuthorizedLeague(currentUser, leagueId);
            if (league == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .Include(u => u.Rankings)
                .FirstOrDefaultAsync(u => u.Id == playerId);
            
            if (user == null)
            {
                return NotFound();
            }

            // Get all matches involving this player
            var matchesToDelete = await _context.Match
                .Where(m => m.LeagueId == leagueId && (m.FirstPlayerId == playerId || m.SecondPlayerId == playerId))
                .Include(m => m.Tournament)
                .OrderByDescending(m => m.Date)
                .ToListAsync();

            // Get tournament players entries
            var tournamentPlayers = await _context.TournamentPlayers
                .Where(tp => tp.PlayerId == playerId)
                .Include(tp => tp.Tournament)
                .ToListAsync();

            var viewModel = new DeletePlayerViewModel
            {
                LeagueId = leagueId,
                LeagueName = league.Name,
                PlayerId = playerId,
                PlayerName = user.DisplayName,
                PlayerUsername = user.UserName,
                MatchCount = matchesToDelete.Count,
                Matches = matchesToDelete,
                TournamentCount = tournamentPlayers.Select(tp => tp.TournamentId).Distinct().Count(),
                RankingCount = user.Rankings?.Count ?? 0
            };

            return View(viewModel);
        }

        // POST: Leagues/DeletePlayerConfirmed
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePlayerConfirmed(Guid leagueId, string playerId)
        {
            var currentUser = await User.GetApplicationUser(_userManager);
            
            // Check if user is admin for this league
            var league = _leaguesRepository.GetAdminAuthorizedLeague(currentUser, leagueId);
            if (league == null)
            {
                return NotFound();
            }

            // Get rankings to delete (needed for clearing PromotionId references)
            var rankingIds = await _context.PlayerRankings
                .Where(r => r.PlayerId == playerId)
                .Select(r => r.RankingId)
                .ToListAsync();

            // Clear PromotionId references in TournamentPlayers that point to this player's rankings
            var tournamentPlayersWithPromotion = await _context.TournamentPlayers
                .Where(tp => tp.PromotionId.HasValue && rankingIds.Contains(tp.PromotionId.Value))
                .ToListAsync();
            foreach (var tp in tournamentPlayersWithPromotion)
            {
                tp.PromotionId = null;
            }

            // Clear CreatedByUserId in matches created by this player (set to null or current admin)
            var matchesCreatedByPlayer = await _context.Match
                .Where(m => m.CreatedByUserId == playerId)
                .ToListAsync();
            foreach (var m in matchesCreatedByPlayer)
            {
                m.CreatedByUserId = currentUser.Id; // Transfer ownership to current admin
            }

            // Delete all matches involving this player in this league
            var matchesToDelete = await _context.Match
                .Where(m => m.LeagueId == leagueId && (m.FirstPlayerId == playerId || m.SecondPlayerId == playerId))
                .ToListAsync();
            _context.Match.RemoveRange(matchesToDelete);

            // Delete all tournament player entries for this player
            var tournamentPlayersToDelete = await _context.TournamentPlayers
                .Where(tp => tp.PlayerId == playerId)
                .ToListAsync();
            _context.TournamentPlayers.RemoveRange(tournamentPlayersToDelete);

            // Delete all player rankings
            var rankingsToDelete = await _context.PlayerRankings
                .Where(r => r.PlayerId == playerId)
                .ToListAsync();
            _context.PlayerRankings.RemoveRange(rankingsToDelete);

            // Delete league player entries
            var leaguePlayers = await _context.LeaguePlayers
                .Where(lp => lp.UserId == playerId)
                .ToListAsync();
            _context.LeaguePlayers.RemoveRange(leaguePlayers);

            // Delete invites sent by this player OR created for this player
            var invites = await _context.Invites
                .Where(i => i.InvitedById == playerId || i.CreatedUserId == playerId)
                .ToListAsync();
            _context.Invites.RemoveRange(invites);

            // Transfer league ownership if this player created any leagues
            var leaguesCreatedByPlayer = await _context.League
                .Where(l => l.CreatedByUserId == playerId)
                .ToListAsync();
            foreach (var l in leaguesCreatedByPlayer)
            {
                l.CreatedByUserId = currentUser.Id; // Transfer ownership to current admin
            }

            // Save changes first before deleting user (to clear all references)
            await _context.SaveChangesAsync();

            // Delete the user account using UserManager (handles Identity tables properly)
            var user = await _userManager.FindByIdAsync(playerId);
            if (user != null)
            {
                await _userManager.DeleteAsync(user);
            }

            // Invalidate cache for this league
            _cache.Remove($"League_{leagueId}");

            TempData["SuccessMessage"] = "Player and all related data deleted successfully.";
            return RedirectToAction(nameof(Details), new { id = leagueId });
        }

    }
}