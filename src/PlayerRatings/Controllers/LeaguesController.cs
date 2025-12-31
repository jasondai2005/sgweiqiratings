using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
        private readonly IWebHostEnvironment _env;

        public LeaguesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            ILeaguesRepository leaguesRepository, IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _leaguesRepository = leaguesRepository;
            _env = env;
        }

        // Match type constants
        private const string MATCH_SWA = "SWA ";
        private const string MATCH_TGA = "TGA ";
        private const string MATCH_SG = "SG ";

        /// <summary>
        /// Filters matches based on date and match type.
        /// For international leagues, all matches are included.
        /// </summary>
        /// <param name="includeDate">If true, includes matches on the exact date (<=). If false, excludes them (&lt;).</param>
        /// <param name="isIntlLeague">If true, includes all matches regardless of match type.</param>
        private static IOrderedEnumerable<Match> FilterMatches(IEnumerable<Match> matches, DateTimeOffset date, bool swaOnly, bool includeDate = true, bool isIntlLeague = false)
        {
            Func<Match, bool> dateFilter = includeDate 
                ? x => x.Date <= date 
                : x => x.Date < date;
            
            // International leagues include all matches
            if (isIntlLeague)
                return matches.Where(x => dateFilter(x)).OrderBy(m => m.Date);
            
            return swaOnly
                ? matches.Where(x => dateFilter(x) && x.MatchName.Contains(MATCH_SWA)).OrderBy(m => m.Date)
                : matches.Where(x => dateFilter(x) && 
                    (x.MatchName.Contains(MATCH_SWA) || x.MatchName.Contains(MATCH_TGA) || x.MatchName.Contains(MATCH_SG))).OrderBy(m => m.Date);
        }

        /// <summary>
        /// Filters matches for a specific player based on date and match type.
        /// For international leagues, all matches are included.
        /// </summary>
        private static IOrderedEnumerable<Match> FilterPlayerMatches(IEnumerable<Match> matches, string playerId, bool swaOnly, bool isIntlLeague = false)
        {
            Func<Match, bool> playerFilter = m => m.FirstPlayerId == playerId || m.SecondPlayerId == playerId;
            
            // International leagues include all matches
            if (isIntlLeague)
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
                players = allPlayers.Where(x => x.User.IsLocalPlayer).ToList();
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
        public async Task<IActionResult> Rating(Guid? id, string byDate, bool swaOnly = false, bool promotionBonus = true, bool showNonLocal = true)
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

            // Load league with all required data in a single query
            // Note: Cannot use AsNoTracking() here because the code modifies entity properties
            // (MatchCount, FirstMatch, LastMatch etc.) and relies on entity identity
            var league = _context.League
                    .Include(l => l.Matches)
                    .ThenInclude(m => m.FirstPlayer)
                    .ThenInclude(p => p.Rankings)
                    .Include(l => l.Matches)
                    .ThenInclude(m => m.SecondPlayer)
                    .ThenInclude(p => p.Rankings)
                    .SingleOrDefault(m => m.Id == id);

            if (league == null)
            {
                return NotFound();
            }

            // Get player statuses for filtering
            var leaguePlayers = _context.LeaguePlayers.AsNoTracking().Where(lp => lp.LeagueId == league.Id).ToList();
            var notBlockedUserIds = new HashSet<string>(
                leaguePlayers.Where(lp => lp.Status != PlayerStatus.Blocked).Select(lp => lp.UserId));
            var hiddenUserIds = new HashSet<string>(
                leaguePlayers.Where(lp => lp.Status == PlayerStatus.Hidden).Select(lp => lp.UserId));
            var alwaysShownUserIds = new HashSet<string>(
                leaguePlayers.Where(lp => lp.Status == PlayerStatus.AlwaysShown).Select(lp => lp.UserId));
            var activeUsers = new HashSet<ApplicationUser>();
            elo = new EloStat();
            var stats = new List<IStat>
            {
                elo,
                new WinRateStat()
            };

            // Check if this is an international league (all matches count, no filtering)
            bool isIntlLeague = league.Name?.Contains("Intl.") ?? false;

            // Set promotion bonus enabled based on parameter
            EloStat.PromotionBonusEnabled = promotionBonus;
            EloStat.SwaOnly = swaOnly;

            // Filter matches based on swaOnly toggle (ignored for international leagues)
            var matches = FilterMatches(league.Matches, date, swaOnly, isIntlLeague: isIntlLeague);
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

            // Check promotions for all active users at the current date
            // This ensures promotions are applied even if the player hasn't played recently
            foreach (var user in activeUsers)
            {
                elo.CheckPlayerPromotion(user, date, isIntlLeague);
            }

            // Finalize any pending operations (e.g., promotion rating floors)
            elo.FinalizeProcessing();

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
            // Also exclude hidden players (Status == Hidden) from display
            var users = activeUsers
                .Where(x => league.Name.Contains("Intl.") || (!x.IsHiddenPlayer))
                .Where(x => !hiddenUserIds.Contains(x.Id))
                .ToList();
            
            // Separate inactive users (not pro)
            var inactiveUsers = users.Where(x => !x.Active && !x.IsProPlayer).ToList();
            
            // Add AlwaysShown users who may not have played any matches
            var activeUserIds = new HashSet<string>(activeUsers.Select(u => u.Id));
            var missingAlwaysShownIds = alwaysShownUserIds.Where(id => !activeUserIds.Contains(id)).ToList();
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
            
            // For Singapore Weiqi league, separate local and non-local players
            bool isSgLeague = league.Name?.Contains("Singapore Weiqi") ?? false;
            List<ApplicationUser> nonLocalUsers = new List<ApplicationUser>();
            if (isSgLeague)
            {
                nonLocalUsers = users.Where(x => !x.IsLocalPlayer).ToList();
                nonLocalUsers.Sort(CompareByRatingAndName);
                users = users.Where(x => x.IsLocalPlayer).ToList();
            }
            
            users.Sort(CompareByRatingAndName);

            var promotedPlayers = activeUsers
                .Where(x => !hiddenUserIds.Contains(x.Id)) // Don't show hidden players in promotions
                .Where(x => (date.Year > 2023 || x.IsHiddenPlayer) && x.Promotion.Contains('→'))
                .ToList();
            promotedPlayers.Sort(CompareByRankingRatingAndName);

            return View(new RatingViewModel(stats, users, promotedPlayers, lastMatches, forecast, id.Value, byDate, swaOnly, isIntlLeague, promotionBonus, nonLocalUsers, showNonLocal, inactiveUsers));
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
        public async Task<IActionResult> Player(Guid id, string playerId, bool swaOnly = false, bool promotionBonus = true)
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

            // Load league with all required data in a single query
            // Note: Cannot use AsNoTracking() here because the code modifies entity properties
            // (MatchCount, FirstMatch, LastMatch etc.) and relies on entity identity
            var league = _context.League
                .Include(l => l.Matches).ThenInclude(m => m.FirstPlayer).ThenInclude(p => p.Rankings)
                .Include(l => l.Matches).ThenInclude(m => m.SecondPlayer).ThenInclude(p => p.Rankings)
                .SingleOrDefault(m => m.Id == id);

            if (league == null)
            {
                return NotFound();
            }

            // Check if this is an international league
            bool isIntlLeague = league.Name?.Contains("Intl.") ?? false;

            // Set promotion bonus enabled based on parameter
            EloStat.PromotionBonusEnabled = promotionBonus;
            EloStat.SwaOnly = swaOnly;

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
            var playerMatches = FilterPlayerMatches(league.Matches, playerId, swaOnly, isIntlLeague).ToList();

            // If player has no matches, show page with just their info (no rating history)
            if (!playerMatches.Any())
            {
                return View(new PlayerRatingHistoryViewModel
                {
                    LeagueId = league.Id,
                    Player = player,
                    MonthlyRatings = new List<MonthlyRating>(),
                    SwaOnly = swaOnly,
                    PromotionBonus = promotionBonus,
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
            
            // ===== OPTIMIZED: Single pass through all matches =====
            // Instead of recalculating from scratch for each month, we process matches once
            // and capture snapshots at month boundaries.
            
            var monthlyRatings = new List<MonthlyRating>();
            var activeUsers = new HashSet<ApplicationUser>();
            var eloStat = new EloStat();
            
            // Get all matches up to current date, sorted chronologically
            League.CutoffDate = DateTimeOffset.Now;
            var allMatches = FilterMatches(league.Matches, League.CutoffDate, swaOnly, includeDate: false, isIntlLeague: isIntlLeague).ToList();
            
            // Track monthly data as we process
            var startMonth = new DateTime(startDate.Year, startDate.Month, 1);
            var currentProcessingMonth = new DateTime(1900, 1, 1); // Will be set on first match
            int playerMatchCount = 0;
            int matchesInCurrentMonth = 0;
            var matchNamesInCurrentMonth = new List<string>();
            
            // Helper to capture monthly snapshot
            void CaptureMonthlySnapshot(DateTime monthToCapture)
            {
                // Only capture if we've reached the start month and player has enough games
                if (monthToCapture < startMonth)
                    return;
                    
                bool hasEnoughGames = !isNewForeignPlayer || playerMatchCount >= 12;
                if (playerMatchCount > 0 && hasEnoughGames)
                {
                    // Check promotion at end of this month
                    var lastDayOfMonth = monthToCapture.AddMonths(1).AddDays(-1);
                    eloStat.CheckPlayerPromotion(player, new DateTimeOffset(lastDayOfMonth), isIntlLeague);
                    eloStat.FinalizeProcessing();
                    
                    matchNamesInCurrentMonth.Reverse();
                    monthlyRatings.Add(new MonthlyRating
                    {
                        Month = monthToCapture,
                        Rating = eloStat[player],
                        MatchesInMonth = matchesInCurrentMonth,
                        MatchNames = new List<string>(matchNamesInCurrentMonth)
                    });
                }
            }
            
            // Process all matches in chronological order (single pass)
            foreach (var match in allMatches)
            {
                var matchMonth = new DateTime(match.Date.Year, match.Date.Month, 1);
                
                // When month changes, capture snapshot of previous month
                if (matchMonth > currentProcessingMonth && currentProcessingMonth.Year > 1900)
                {
                    CaptureMonthlySnapshot(currentProcessingMonth);
                    
                    // Fill in any skipped months (player inactive)
                    var nextMonth = currentProcessingMonth.AddMonths(1);
                    while (nextMonth < matchMonth)
                    {
                        CaptureMonthlySnapshot(nextMonth);
                        nextMonth = nextMonth.AddMonths(1);
                    }
                    
                    // Reset monthly counters
                    matchesInCurrentMonth = 0;
                    matchNamesInCurrentMonth.Clear();
                }
                currentProcessingMonth = matchMonth;
                
                // Track user activity (same as AddUser in Rating page)
                AddUser(activeUsers, match, match.FirstPlayer);
                AddUser(activeUsers, match, match.SecondPlayer);
                
                // Add match to ELO calculation
                eloStat.AddMatch(match);
                
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
            
            // Capture final month and any remaining months up to current
            if (currentProcessingMonth.Year > 1900)
            {
                CaptureMonthlySnapshot(currentProcessingMonth);
                
                // Fill in months up to current month
                var endMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var nextMonth = currentProcessingMonth.AddMonths(1);
                while (nextMonth <= endMonth)
                {
                    matchesInCurrentMonth = 0;
                    matchNamesInCurrentMonth.Clear();
                    CaptureMonthlySnapshot(nextMonth);
                    nextMonth = nextMonth.AddMonths(1);
                }
            }
            
            // Check promotions for all active users at current date
            foreach (var user in activeUsers)
            {
                eloStat.CheckPlayerPromotion(user, League.CutoffDate, isIntlLeague);
            }
            eloStat.FinalizeProcessing();
            
            // Calculate position from the same EloStat (no need to recalculate)
            bool isSgLeague = league.Name?.Contains("Singapore Weiqi") ?? false;
            var rankedUsers = activeUsers
                .Where(x => x.Active)
                .Where(x => !x.IsProPlayer)
                .Where(x => isIntlLeague || !x.IsHiddenPlayer)
                .Where(x => !isSgLeague || x.IsLocalPlayer)
                .ToList();
            
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
            
            // Reset player data for consistency
            foreach (var match in allMatches)
            {
                match.FirstPlayer.MatchCount = 0;
                match.FirstPlayer.FirstMatch = DateTimeOffset.MinValue;
                match.FirstPlayer.LastMatch = DateTimeOffset.MinValue;
                match.FirstPlayer.PreviousMatchDate = DateTimeOffset.MinValue;
                match.FirstPlayer.MatchesSinceReturn = 0;
                match.SecondPlayer.MatchCount = 0;
                match.SecondPlayer.FirstMatch = DateTimeOffset.MinValue;
                match.SecondPlayer.LastMatch = DateTimeOffset.MinValue;
                match.SecondPlayer.PreviousMatchDate = DateTimeOffset.MinValue;
                match.SecondPlayer.MatchesSinceReturn = 0;
            }

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
                IsIntlLeague = isIntlLeague,
                PromotionBonus = promotionBonus,
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