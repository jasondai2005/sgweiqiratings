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

        // Match type constants
        private const string MATCH_SWA = "SWA ";
        private const string MATCH_TGA = "TGA ";
        private const string MATCH_SG = "SG ";

        // Minimum date for rating calculations (matches before this date are not included in ratings)
        private static readonly DateTimeOffset RATING_START_DATE = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Cache duration for league data (5 minutes)
        private static readonly TimeSpan LeagueCacheDuration = TimeSpan.FromMinutes(5);

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
        /// Filters matches based on date and match type for rating calculations.
        /// Excludes matches before RATING_START_DATE (01/01/2023).
        /// For non-SG leagues (international), all match types are included.
        /// </summary>
        /// <param name="isSgLeague">If true, filters by match type (SWA/TGA/SG). If false, includes all match types.</param>
        private static IOrderedEnumerable<Match> FilterMatches(IEnumerable<Match> matches, DateTimeOffset date, bool swaOnly, bool isSgLeague = true)
        {
            // Date filter: between RATING_START_DATE and cutoff date
            Func<Match, bool> dateFilter = x => x.Date >= RATING_START_DATE && x.Date <= date;
            
            // Non-SG (international) leagues include all match types
            if (!isSgLeague)
                return matches.Where(x => dateFilter(x)).OrderBy(m => m.Date);
            
            return swaOnly
                ? matches.Where(x => dateFilter(x) && x.MatchName.Contains(MATCH_SWA)).OrderBy(m => m.Date)
                : matches.Where(x => dateFilter(x) && 
                    (x.MatchName.Contains(MATCH_SWA) || x.MatchName.Contains(MATCH_TGA) || x.MatchName.Contains(MATCH_SG))).OrderBy(m => m.Date);
        }

        /// <summary>
        /// Filters matches for a specific player based on date and match type.
        /// For non-SG leagues (international), all matches are included.
        /// </summary>
        private static IOrderedEnumerable<Match> FilterPlayerMatches(IEnumerable<Match> matches, string playerId, bool swaOnly, bool isSgLeague = true)
        {
            Func<Match, bool> playerFilter = m => m.FirstPlayerId == playerId || m.SecondPlayerId == playerId;
            
            // Non-SG (international) leagues include all matches
            if (!isSgLeague)
                return matches.Where(m => playerFilter(m)).OrderBy(m => m.Date);
            
            return swaOnly
                ? matches.Where(m => playerFilter(m) && m.MatchName.Contains(MATCH_SWA)).OrderBy(m => m.Date)
                : matches.Where(m => playerFilter(m) && 
                    (m.MatchName.Contains(MATCH_SWA) || m.MatchName.Contains(MATCH_TGA) || m.MatchName.Contains(MATCH_SG))).OrderBy(m => m.Date);
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

            var allPlayers = _context.LeaguePlayers
                .AsNoTracking()
                .Include(lp => lp.User).ThenInclude(u => u.Rankings)
                .Where(lp =>
                    lp.LeagueId == league.Id &&
                    !lp.User.DisplayName.Contains("[") &&
                    lp.User.DisplayName != "Admin").ToList();
            if (Elo.SwaRankedPlayersOnly)
                allPlayers = allPlayers.Where(x => x.User.LatestSwaRanking.Any()).ToList();
            
            // For Singapore Weiqi league, separate local and non-local players
            bool isSgLeague = league.Name?.Contains("Singapore Weiqi") ?? false;
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
        public async Task<IActionResult> Rating(Guid? id, string byDate, bool swaOnly = false, bool refresh = false)
        {
            if (id == null)
            {
                return NotFound();
            }
            var date = byDate == null ? DateTimeOffset.Now : DateTimeOffset.ParseExact(byDate, "dd/MM/yyyy", null);
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

            // Get player statuses for filtering
            var (notBlockedUserIds, hiddenUserIds, alwaysShownUserIds) = GetPlayerStatuses(league.Id);

            // Use shared calculation method - also collect matches for lastMatches display
            var winRateStat = new WinRateStat();
            var recentMatches = new List<Match>();
            var (eloResult, activeUsers, isSgLeague) = CalculateRatings(
                league, date, swaOnly,
                allowedUserIds: notBlockedUserIds,
                onMatchProcessed: (match, _) => {
                    winRateStat.AddMatch(match);
                    if (match.Date > date.AddMonths(-1))
                        recentMatches.Add(match);
                });
            elo = eloResult;
            
            var stats = new List<IStat> { elo, winRateStat };

            // Build forecast matrix
            var userList = new List<ApplicationUser>(activeUsers);
            var forecast = new Dictionary<string, Dictionary<string, string>>();
            //foreach (var appUser in userList)
            //{
            //    var dict = new Dictionary<string, string>();
            //    var userRating = elo[appUser];
            //    foreach (var t in userList)
            //    {
            //        dict[t.Id] = (new Elo(userRating, elo[t], 1, 0).NewRatingAPlayer - userRating).ToString("N1");
            //    }
            //    forecast[appUser.Id] = dict;
            //}

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

            // Calculate comparison data (first day of selected month, or previous month if already 1st)
            var previousRatings = new Dictionary<string, double>();
            var previousPositions = new Dictionary<string, int>();
            DateTimeOffset? comparisonDate = null;
            
            // If it's the 1st of the month, compare to the 1st of previous month
            // Otherwise, compare to the 1st of current month
            comparisonDate = date.Day == 1 
                ? new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, date.Offset).AddMonths(-1)
                : new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, date.Offset);
            
            // Run calculation for comparison date
            var (prevEloStat, prevActiveUsers, _) = CalculateRatings(
                league, comparisonDate.Value, swaOnly,
                allowedUserIds: notBlockedUserIds);
            
            // Build previous ratings dictionary
            foreach (var user in prevActiveUsers)
            {
                previousRatings[user.Id] = prevEloStat[user];
            }
            
            // Build previous positions (same filtering as current)
            var prevUsers = prevActiveUsers
                .Where(x => (!isSgLeague || !x.IsHiddenPlayer) && !hiddenUserIds.Contains(x.Id))
                .Where(x => x.Active || x.IsProPlayer)
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

            return View(new RatingViewModel(stats, users, promotedPlayers, recentMatches, forecast, id.Value, byDate, swaOnly, isSgLeague, nonLocalUsers, inactiveUsers, previousRatings, previousPositions, comparisonDate));
        }

        private static void AddUser(HashSet<ApplicationUser> activeUsers, Match match, ApplicationUser player)
        {
            if (player.FirstMatch == DateTimeOffset.MinValue)
            {
                player.FirstMatch = match.Date;
            }

            // Check for 2+ year gap (returning inactive player)
            if (player.LastMatch != DateTimeOffset.MinValue)
            {
                var gap = match.Date - player.LastMatch;
                if (gap.TotalDays > 365 * 2)
                {
                    // Detected a 2+ year gap, reset the counter
                    player.MatchesSinceReturn = 1;
                }
                else if (player.MatchesSinceReturn > 0)
                {
                    // Already tracking since return, increment counter
                    player.MatchesSinceReturn++;
                }
            }

            player.PreviousMatchDate = player.LastMatch;
            player.LastMatch = match.Date;
            player.MatchCount++;

            activeUsers.Add(player);
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
        /// Ensures consistent results across both views.
        /// </summary>
        /// <param name="league">League with matches loaded</param>
        /// <param name="cutoffDate">Date to calculate ratings up to</param>
        /// <param name="swaOnly">Filter for SWA tournaments only</param>
        /// <param name="allowedUserIds">Optional filter - only include these user IDs (null = include all)</param>
        /// <param name="onMatchProcessed">Optional callback for each match with EloStat (for monthly snapshots)</param>
        /// <returns>Tuple of (EloStat, activeUsers, isSgLeague)</returns>
        private (EloStat eloStat, HashSet<ApplicationUser> activeUsers, bool isSgLeague) 
            CalculateRatings(
                League league, 
                DateTimeOffset cutoffDate, 
                bool swaOnly,
                HashSet<string> allowedUserIds = null,
                Action<Match, EloStat> onMatchProcessed = null)
        {
            // Reset player transient state at the start (important for cached data)
            ResetPlayerState(league.Matches);

            League.CutoffDate = cutoffDate;
            EloStat.SwaOnly = swaOnly;

            bool isSgLeague = league.Name?.Contains("Singapore Weiqi") ?? false;

            var activeUsers = new HashSet<ApplicationUser>();
            var eloStat = new EloStat();

            var matches = FilterMatches(league.Matches, cutoffDate, swaOnly, isSgLeague: isSgLeague);
            foreach (var match in matches)
            {
                // Add users (with optional filter)
                if (allowedUserIds == null || allowedUserIds.Contains(match.FirstPlayerId))
                {
                    AddUser(activeUsers, match, match.FirstPlayer);
                }
                if (allowedUserIds == null || allowedUserIds.Contains(match.SecondPlayerId))
                {
                    AddUser(activeUsers, match, match.SecondPlayer);
                }

                eloStat.AddMatch(match);
                
                // Callback for additional processing (e.g., monthly snapshots)
                onMatchProcessed?.Invoke(match, eloStat);
            }

            // Check promotions for all active users
            foreach (var user in activeUsers)
            {
                eloStat.CheckPlayerPromotion(user, cutoffDate, isSgLeague);
            }
            eloStat.FinalizeProcessing();

            return (eloStat, activeUsers, isSgLeague);
        }

        /// <summary>
        /// Resets player transient state before rating calculation.
        /// This is important when using cached data to ensure each calculation starts fresh.
        /// </summary>
        private static void ResetPlayerState(IEnumerable<Match> matches)
        {
            var resetPlayers = new HashSet<string>();
            foreach (var match in matches)
            {
                if (resetPlayers.Add(match.FirstPlayerId))
                {
                    ResetPlayer(match.FirstPlayer);
                }
                if (resetPlayers.Add(match.SecondPlayerId))
                {
                    ResetPlayer(match.SecondPlayer);
                }
            }
        }

        private static void ResetPlayer(ApplicationUser player)
        {
            player.MatchCount = 0;
            player.FirstMatch = DateTimeOffset.MinValue;
            player.LastMatch = DateTimeOffset.MinValue;
            player.PreviousMatchDate = DateTimeOffset.MinValue;
            player.MatchesSinceReturn = 0;
            player.EstimatedInitialRating = null;
        }

        /// <summary>
        /// Gets ranked users list from active users (same logic for Rating and Player pages).
        /// </summary>
        /// <param name="cutoffDate">Date to check local player status against</param>
        private List<ApplicationUser> GetRankedUsers(
            HashSet<ApplicationUser> activeUsers, 
            HashSet<string> hiddenUserIds,
            bool isSgLeague,
            DateTimeOffset cutoffDate)
        {
            return activeUsers
                .Where(x => x.Active 
                    && !x.IsProPlayer
                    && (!isSgLeague || !x.IsHiddenPlayer) // SG league hides monitoring players
                    && (hiddenUserIds == null || !hiddenUserIds.Contains(x.Id))
                    && (!isSgLeague || x.IsLocalPlayerAt(cutoffDate)))
                .ToList();
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
            var result1 = elo.GetResult(x);
            var result2 = elo.GetResult(y);
            
            // Handle users with no rating (empty string from GetResult) - use their ranking rating instead
            if (!double.TryParse(result1, out double rating1))
                rating1 = x.GetRatingByRanking(ranking1);
            if (!double.TryParse(result2, out double rating2))
                rating2 = y.GetRatingByRanking(ranking2);
            
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
        public async Task<IActionResult> Player(Guid id, string playerId, bool swaOnly = false, bool refresh = false)
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
                player = await _context.Users
                    .Include(u => u.Rankings)
                    .FirstOrDefaultAsync(u => u.Id == playerId);
                
                if (player == null)
                {
                    return NotFound();
                }
            }

            // Find player's matches (all matches for intl leagues, filtered for local leagues)
            var playerMatches = FilterPlayerMatches(league.Matches, playerId, swaOnly, isSgLeague).ToList();

            // If player has no matches, show page with just their info (no rating history)
            if (!playerMatches.Any())
            {
                return View(new PlayerRatingHistoryViewModel
                {
                    LeagueId = league.Id,
                    Player = player,
                    MonthlyRatings = new List<MonthlyRating>(),
                    SwaOnly = swaOnly,
                    GameRecords = new List<GameRecord>()
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
            var startMonth = new DateTime(startDate.Year, startDate.Month, 1);
            var currentProcessingMonth = new DateTime(1900, 1, 1);
            int playerMatchCount = 0;
            int matchesInCurrentMonth = 0;
            var matchNamesInCurrentMonth = new List<string>();
            EloStat currentEloStat = null;
            
            // Callback to capture monthly snapshots during processing
            void OnMatchProcessed(Match match, EloStat elo)
            {
                currentEloStat = elo;
                var matchMonth = new DateTime(match.Date.Year, match.Date.Month, 1);
                
                // When month changes, capture snapshot of previous month
                if (matchMonth > currentProcessingMonth && currentProcessingMonth.Year > 1900)
                {
                    CaptureSnapshot(currentProcessingMonth);
                    
                    // Fill in any skipped months
                    var nextMonth = currentProcessingMonth.AddMonths(1);
                    while (nextMonth < matchMonth)
                    {
                        CaptureSnapshot(nextMonth);
                        nextMonth = nextMonth.AddMonths(1);
                    }
                    
                    matchesInCurrentMonth = 0;
                    matchNamesInCurrentMonth.Clear();
                }
                currentProcessingMonth = matchMonth;
                
                // Track this player's monthly stats
                if (match.FirstPlayerId == playerId || match.SecondPlayerId == playerId)
                {
                    playerMatchCount++;
                    matchesInCurrentMonth++;
                    if (!matchNamesInCurrentMonth.Contains(match.MatchName))
                    {
                        matchNamesInCurrentMonth.Add(match.MatchName);
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
                    var reversed = new List<string>(matchNamesInCurrentMonth);
                    reversed.Reverse();
                    monthlyRatings.Add(new MonthlyRating
                    {
                        Month = monthToCapture,
                        Rating = currentEloStat[player],
                        MatchesInMonth = matchesInCurrentMonth,
                        MatchNames = reversed
                    });
                }
            }
            
            // Use shared calculation (same as Rating page)
            var (eloStat, activeUsers, _) = CalculateRatings(
                league, 
                DateTimeOffset.Now, 
                swaOnly,
                allowedUserIds: notBlockedUserIds,
                onMatchProcessed: OnMatchProcessed);
            currentEloStat = eloStat;
            
            // Capture final month and fill remaining months
            if (currentProcessingMonth.Year > 1900)
            {
                CaptureSnapshot(currentProcessingMonth);
                
                var endMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var nextMonth = currentProcessingMonth.AddMonths(1);
                while (nextMonth <= endMonth)
                {
                    matchesInCurrentMonth = 0;
                    matchNamesInCurrentMonth.Clear();
                    CaptureSnapshot(nextMonth);
                    nextMonth = nextMonth.AddMonths(1);
                }
            }
            
            // Get ranked users using shared method (same filtering as Rating page)
            var rankedUsers = GetRankedUsers(activeUsers, hiddenUserIds, isSgLeague, DateTimeOffset.Now);
            
            rankedUsers.Sort((x, y) =>
            {
                double ratingX = eloStat[x];
                double ratingY = eloStat[y];
                int result = ratingY.CompareTo(ratingX);
                if (result == 0)
                {
                    var ranking1 = x.GetCombinedRankingBeforeDate(League.CutoffDate);
                    var ranking2 = y.GetCombinedRankingBeforeDate(League.CutoffDate);
                    if (ranking1 != ranking2)
                        return string.Compare(ranking1, ranking2, StringComparison.OrdinalIgnoreCase);
                    return string.Compare(x.DisplayName, y.DisplayName, StringComparison.OrdinalIgnoreCase);
                }
                return result;
            });
            
            int totalPlayers = rankedUsers.Count;
            int position = rankedUsers.FindIndex(u => u.Id == playerId) + 1;

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
                
                // Get opponent ranking at time of match
                // Use just the date (midnight) to exclude same-day promotions
                // since promotions happen at the end of the day after all matches
                var opponentRanking = opponent.GetCombinedRankingBeforeDate(match.Date.Date);
                
                gameRecords.Add(new GameRecord
                {
                    Date = match.Date,
                    MatchName = match.MatchName,
                    OpponentName = opponent.DisplayName,
                    OpponentRanking = opponentRanking,
                    OpponentId = opponent.Id,
                    Result = result
                });
            }

            return View(new PlayerRatingHistoryViewModel
            {
                Player = player,
                LeagueId = id,
                MonthlyRatings = monthlyRatings,
                GameRecords = gameRecords,
                SwaOnly = swaOnly,
                IsSgLeague = isSgLeague,
                Position = position,
                TotalPlayers = totalPlayers
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
                RankingNote = model.RankingNote
            };

            _context.PlayerRankings.Add(ranking);
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

            ranking.Ranking = model.Ranking.ToUpper();
            ranking.Organization = model.Organization == "Other" ? null : model.Organization;
            ranking.RankingDate = model.RankingDate.HasValue 
                ? new DateTimeOffset(model.RankingDate.Value, TimeSpan.Zero) 
                : null;
            ranking.RankingNote = model.RankingNote;

            _context.PlayerRankings.Update(ranking);
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

    }
}