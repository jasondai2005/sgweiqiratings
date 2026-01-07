using Microsoft.Extensions.Localization;
using PlayerRatings.Models;
using PlayerRatings.Util;
using PlayerRatings.ViewModels.Match;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Csv;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayerRatings.Localization;
using PlayerRatings.Repositories;
using PlayerRatings.Services;

namespace PlayerRatings.Controllers
{
    [Authorize]
    public class MatchesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<MatchesController> _localizer;
        private readonly IInvitesService _invitesService;
        private readonly ILeaguesRepository _leaguesRepository;

        public MatchesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            IStringLocalizer<MatchesController> localizer, IInvitesService invitesService, ILeaguesRepository leaguesRepository)
        {
            _context = context;
            _userManager = userManager;
            _localizer = localizer;
            _invitesService = invitesService;
            _leaguesRepository = leaguesRepository;
        }

        public async Task<IActionResult> Index(Guid leagueId, int? year = null, int? month = null)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var league = _leaguesRepository.GetUserAuthorizedLeague(currentUser, leagueId);

            if (league == null)
            {
                return NotFound();
            }

            // Get all available months for this league
            var availableMonths = _context.Match
                .AsNoTracking()
                .Where(m => m.LeagueId == leagueId)
                .Select(m => m.Date)
                .ToList()
                .Select(d => new DateTime(d.Year, d.Month, 1))
                .Distinct()
                .OrderByDescending(m => m)
                .ToList();

            if (!availableMonths.Any())
            {
                return View(new MatchesViewModel(new List<Models.Match>(), leagueId, DateTime.Now, availableMonths));
            }

            // Determine current month to display
            DateTime currentMonth;
            if (year.HasValue && month.HasValue)
            {
                currentMonth = new DateTime(year.Value, month.Value, 1);
                if (!availableMonths.Contains(currentMonth))
                {
                    // If the requested month has no matches, redirect to the latest month
                    currentMonth = availableMonths.First();
                }
            }
            else
            {
                // Default to the most recent month with matches
                currentMonth = availableMonths.First();
            }

            var monthStart = new DateTimeOffset(currentMonth, TimeSpan.Zero);
            var monthEnd = new DateTimeOffset(currentMonth.AddMonths(1), TimeSpan.Zero);

            var matches =
                _context.Match
                    .AsNoTracking()
                    .Where(m => m.LeagueId == leagueId && m.Date >= monthStart && m.Date < monthEnd)
                    .OrderByDescending(m => m.Date)
                    .Include(m => m.FirstPlayer).ThenInclude(p => p.Rankings)
                    .Include(m => m.SecondPlayer).ThenInclude(p => p.Rankings)
                    .Include(m => m.League)
                    .Include(m => m.Tournament)
                    .ToList();

            return View(new MatchesViewModel(matches, leagueId, currentMonth, availableMonths));
        }

        private ICollection<League> GetLeagues(ApplicationUser currentUser, Guid? leagueId)
        {
            var query = _context.LeaguePlayers.Where(lp => lp.UserId == currentUser.Id);
            if (leagueId.HasValue)
            {
                var id = leagueId.Value;
                query = query.Where(lp => lp.LeagueId == id);
            }
            return query
                    .Select(lp => lp.League)
                    .Distinct()
                    .ToList();
        }

        private Dictionary<ApplicationUser, IEnumerable<Guid>> GetUsers(IEnumerable<Guid> leagueIds)
        {
            // Exclude only Blocked players from match entry
            return _context.LeaguePlayers
                .Where(lp => leagueIds.Contains(lp.LeagueId) && lp.Status != Models.PlayerStatus.Blocked)
                .Include(lp => lp.User)
                .ToList()
                .GroupBy(lp => lp.User)
                .ToDictionary(g => g.Key, g => g.Select(lp => lp.LeagueId).ToList().AsEnumerable());
        }

        // GET: /<controller>/
        public async Task<IActionResult> Create(Guid? leagueId, DateTimeOffset? lastMatchDateTime, string matchName, double? factor, Guid? tournamentId, int? round, string firstPlayerId = null, string secondPlayerId = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            var leagues = GetLeagues(currentUser, null);

            if (!leagues.Any())
            {
                return RedirectToAction("NoLeagues", "Leagues");
            }

            var leagueIds = leagues.Select(l => l.Id).ToList();
            var players = GetUsers(leagueIds);

            // Load tournaments for all leagues the user has access to
            var tournaments = _context.Tournaments
                .Where(t => leagueIds.Contains(t.LeagueId))
                .OrderByDescending(t => t.StartDate)
                .ToList()
                .Select(t => new ViewModels.Match.TournamentSelectItem
                {
                    Id = t.Id,
                    LeagueId = t.LeagueId,
                    Name = t.FullName
                })
                .ToList();

            // Determine first player - use provided or default to current user
            var effectiveFirstPlayerId = !string.IsNullOrEmpty(firstPlayerId) ? firstPlayerId : currentUser.Id;
            
            // Determine second player - use provided, or default to first available
            // "BYE" marker value means select BYE (empty string in dropdown)
            // null means use default (first available player)
            string effectiveSecondPlayerId;
            if (secondPlayerId == "BYE")
                effectiveSecondPlayerId = "";  // Empty string selects BYE option in dropdown
            else if (!string.IsNullOrEmpty(secondPlayerId))
                effectiveSecondPlayerId = secondPlayerId;
            else
                effectiveSecondPlayerId = players.Keys.FirstOrDefault(p => p.Id != effectiveFirstPlayerId)?.Id;

            return View("Create", new NewResultViewModel(leagues, players, lastMatchDateTime, matchName, factor)
            {
                LeagueId = leagueId ?? leagues.First().Id,
                FirstPlayerId = effectiveFirstPlayerId,
                SecondPlayerId = effectiveSecondPlayerId,
                Tournaments = tournaments,
                TournamentId = tournamentId,
                Round = round
            });
        }

        /// <summary>
        /// Verifies that passed user is visible for current user and adds to league
        /// </summary>
        /// <param name="playerId">New league player id</param>
        /// <param name="league">League</param>
        /// <returns>League player or null</returns>
        private async Task<ApplicationUser> AddToLeague(string playerId, League league)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var leagues = GetLeagues(currentUser, league.Id);

            var leagueIds = leagues.Select(l => l.Id).ToList();
            //We use GetUsers to verify that invitee is visible for current user
            var player = GetUsers(leagueIds).Keys.FirstOrDefault(p => p.Id == playerId);

            if (player != null && !_context.LeaguePlayers.Any(lp => lp.LeagueId == league.Id && lp.UserId == player.Id))
            {
                _context.LeaguePlayers.Add(new LeaguePlayer
                {
                    Id = Guid.NewGuid(),
                    League = league,
                    User = player
                });

                _context.SaveChanges();
            }

            return player;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(NewResultViewModel model, bool toRating)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            if (ModelState.IsValid)
            {
                var league = _leaguesRepository.GetUserAuthorizedLeague(currentUser, model.LeagueId);

                if (league == null)
                {
                    ModelState.AddModelError("", _localizer[nameof(LocalizationKey.LeagueNotFound)]);
                    var allLeagues = GetLeagues(currentUser, null);
                    var allLeagueIds = allLeagues.Select(l => l.Id).ToList();
                    model.Leagues = allLeagues;
                    model.Users = GetUsers(allLeagueIds);
                    model.Tournaments = _context.Tournaments
                        .Where(t => allLeagueIds.Contains(t.LeagueId))
                        .OrderByDescending(t => t.StartDate)
                        .ToList()
                        .Select(t => new ViewModels.Match.TournamentSelectItem
                        {
                            Id = t.Id,
                            LeagueId = t.LeagueId,
                            Name = t.FullName
                        })
                        .ToList();
                    return View("Create", model);
                }

                var players = GetUsers(new[] { league.Id });

                var firstPlayer = players.Keys.FirstOrDefault(p => p.Id == model.FirstPlayerId) ??
                    await AddToLeague(model.FirstPlayerId, league);
                
                // SecondPlayer can be NULL for BYE matches
                ApplicationUser secondPlayer = null;
                if (!string.IsNullOrEmpty(model.SecondPlayerId))
                {
                    secondPlayer = players.Keys.FirstOrDefault(p => p.Id == model.SecondPlayerId) ??
                        await AddToLeague(model.SecondPlayerId, league);
                }

                // First player is required, second player can be NULL (BYE)
                if (firstPlayer == null)
                {
                    ModelState.AddModelError("", _localizer[nameof(LocalizationKey.PlayerNotFound)]);
                    model.Leagues = new[] {league};
                    model.Users = players;
                    return View("Create", model);
                }

                var match = new Match
                {
                    Id = Guid.NewGuid(),
                    Date = model.Date,
                    FirstPlayer = firstPlayer,
                    SecondPlayer = secondPlayer,
                    FirstPlayerScore = model.FirstPlayerScore,
                    SecondPlayerScore = model.SecondPlayerScore,
                    League = league,
                    CreatedByUser = currentUser,
                    MatchName = model.MatchName,
                    Factor = model.Factor == 1 ? null : model.Factor,
                    TournamentId = model.TournamentId,
                    Round = model.Round
                };

                // If tournament is selected, update match name based on tournament info
                if (model.TournamentId.HasValue)
                {
                    var tournament = _context.Tournaments.Find(model.TournamentId.Value);
                    if (tournament != null && tournament.LeagueId == league.Id)
                    {
                        // Build match name: Organizer + Ordinal + Name + Group + Round
                        var nameParts = new List<string>();
                        if (!string.IsNullOrEmpty(tournament.Organizer))
                            nameParts.Add(tournament.Organizer);
                        if (!string.IsNullOrEmpty(tournament.Ordinal))
                            nameParts.Add(tournament.Ordinal);
                        nameParts.Add(tournament.Name);
                        if (!string.IsNullOrEmpty(tournament.Group))
                            nameParts.Add(tournament.Group);
                        if (model.Round.HasValue)
                            nameParts.Add($"R{model.Round.Value}");
                        match.MatchName = string.Join(" ", nameParts);

                        // Use tournament factor if match factor not set
                        if (!match.Factor.HasValue && tournament.Factor.HasValue)
                        {
                            match.Factor = tournament.Factor;
                        }

                        // Add players to tournament if not already there (skip NULL players for bye matches)
                        if (!_context.TournamentPlayers.Any(tp => tp.TournamentId == tournament.Id && tp.PlayerId == firstPlayer.Id))
                        {
                            _context.TournamentPlayers.Add(new TournamentPlayer { TournamentId = tournament.Id, PlayerId = firstPlayer.Id });
                        }
                        if (secondPlayer != null && !_context.TournamentPlayers.Any(tp => tp.TournamentId == tournament.Id && tp.PlayerId == secondPlayer.Id))
                        {
                            _context.TournamentPlayers.Add(new TournamentPlayer { TournamentId = tournament.Id, PlayerId = secondPlayer.Id });
                        }
                    }
                }

                _context.Match.Add(match);
                _context.SaveChanges();
                if (toRating)
                {
                    return RedirectToAction(nameof(LeaguesController.Rating), "Leagues", new {id = model.LeagueId});
                }
                else
                {
                    // Pass round to next match creation for convenience
                    return RedirectToAction(nameof(Create), new { leagueId = model.LeagueId, lastMatchDateTime = model.Date, matchName = model.MatchName, factor = model.Factor, tournamentId = model.TournamentId, round = model.Round });
                }
            }

            model.Leagues = GetLeagues(currentUser, null);
            var availableLeagueIds = model.Leagues.Select(l => l.Id).ToList();
            model.Users = GetUsers(availableLeagueIds);
            model.Tournaments = _context.Tournaments
                .Where(t => availableLeagueIds.Contains(t.LeagueId))
                .OrderByDescending(t => t.StartDate)
                .ToList()
                .Select(t => new ViewModels.Match.TournamentSelectItem
                {
                    Id = t.Id,
                    LeagueId = t.LeagueId,
                    Name = t.FullName
                })
                .ToList();
            return View("Create", model);
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var match = _context.Match.Include(m => m.League).Single(m => m.Id == id);
            if (match == null)
            {
                return NotFound();
            }

            var currentUser = await User.GetApplicationUser(_userManager);

            var league = match.League;

            if (league.CreatedByUserId != currentUser.Id && match.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            var leagues = new[] { match.League };
            var leagueIds = leagues.Select(l => l.Id).ToList();

            // Load tournaments for the league
            var tournaments = await _context.Tournaments
                .Where(t => t.LeagueId == league.Id)
                .OrderByDescending(t => t.StartDate)
                .Select(t => new ViewModels.Match.TournamentSelectItem
                {
                    Id = t.Id,
                    LeagueId = t.LeagueId,
                    Name = t.FullName
                })
                .ToListAsync();

            ViewBag.Editing = true;

            return View("Create", new NewResultViewModel(leagues, GetUsers(leagueIds), null, match.MatchName, match.Factor)
            {
                LeagueId = leagues.First().Id,
                FirstPlayerId = match.FirstPlayerId,
                SecondPlayerId = match.SecondPlayerId,
                Date = match.Date,
                FirstPlayerScore = match.FirstPlayerScore,
                SecondPlayerScore = match.SecondPlayerScore,
                TournamentId = match.TournamentId,
                Round = match.Round,
                Tournaments = tournaments
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, NewResultViewModel model)
        {
            if (ModelState.IsValid)
            {
                var match = _context.Match.Single(m => m.Id == id);
                if (match == null)
                {
                    return NotFound();
                }

                var currentUser = await User.GetApplicationUser(_userManager);

                var league = _leaguesRepository.GetUserAuthorizedLeague(currentUser, model.LeagueId);

                if (league == null)
                {
                    ModelState.AddModelError("", _localizer[nameof(LocalizationKey.LeagueNotFound)]);
                    return View("Create", model);
                }

                var players =
                _context.LeaguePlayers.Where(lp => lp.LeagueId == league.Id).Select(lp => lp.User).ToList();

                var firstPlayer = players.FirstOrDefault(p => p.Id == model.FirstPlayerId);
                
                // SecondPlayer can be NULL for BYE matches
                ApplicationUser secondPlayer = null;
                if (!string.IsNullOrEmpty(model.SecondPlayerId))
                {
                    secondPlayer = players.FirstOrDefault(p => p.Id == model.SecondPlayerId);
                }

                // First player is required, second player can be NULL (BYE)
                if (firstPlayer == null)
                {
                    ModelState.AddModelError("", _localizer[nameof(LocalizationKey.PlayerNotFound)]);
                    return View("Create", model);
                }

                match.FirstPlayer = firstPlayer;
                match.FirstPlayerId = firstPlayer.Id;
                match.SecondPlayer = secondPlayer;
                match.SecondPlayerId = secondPlayer?.Id;
                match.FirstPlayerScore = model.FirstPlayerScore;
                match.SecondPlayerScore = model.SecondPlayerScore;
                match.Date = model.Date;
                match.MatchName = model.MatchName;
                match.Factor = model.Factor == 1 ? null : model.Factor;
                match.TournamentId = model.TournamentId;
                match.Round = model.Round;

                _context.SaveChanges();

                var leagueId = league.Id;

                return RedirectToAction(nameof(Index), new
                {
                    leagueId
                });
            }

            ViewBag.Editing = true;

            return View("Create", model);
        }

        public async Task<IActionResult> Delete(Guid id)
        {
            var match = _context.Match.Include(m => m.League).Include(m => m.FirstPlayer).Include(m => m.SecondPlayer).Single(m => m.Id == id);
            if (match == null)
            {
                return NotFound();
            }

            var currentUser = await User.GetApplicationUser(_userManager);

            var league = match.League;

            if (league.CreatedByUserId != currentUser.Id && match.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            return View(match);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var match = _context.Match.Single(m => m.Id == id);
            if (match == null)
            {
                return NotFound();
            }

            var currentUser = await User.GetApplicationUser(_userManager);

            var league = _leaguesRepository.GetUserAuthorizedLeague(currentUser, match.LeagueId);

            if (league == null)
            {
                return NotFound();
            }

            _context.Match.Remove(match);
            _context.SaveChanges();

            var leagueId = league.Id;

            return RedirectToAction(nameof(Index), new
            {
                leagueId
            });
        }

        public async Task<IActionResult> Import(Guid leagueId)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var league = _leaguesRepository.GetUserAuthorizedLeague(currentUser, leagueId);

            if (league == null)
            {
                return NotFound();
            }

            return View(new ImportViewModel
            {
                DateIndex = 1,
                FirstPlayerEmailIndex = 2,
                FirstPlayerScoreIndex = 3,
                SecondPlayerEmailIndex = 4,
                SecondPlayerScoreIndex = 5,
                LeagueId = leagueId,
                DateTimeFormat = "dd.MM.yyyy H:mm:ss"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(ImportViewModel model)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var league = _leaguesRepository.GetUserAuthorizedLeague(currentUser, model.LeagueId);

            if (league == null)
            {
                return NotFound();
            }

            var matches = new[] { model.File }.SelectMany(f =>
            {
                using (var stream = f.OpenReadStream())
                {
                    return CsvReader.ReadFromStream(stream).Select(line => new
                    {
                        Date = DateTimeOffset.ParseExact(line[model.DateIndex - 1], model.DateTimeFormat, null),
                        FirstPlayerEmail = line[model.FirstPlayerEmailIndex - 1].ToLower().Trim(),
                        FirstPlayerScore = Convert.ToInt32(line[model.FirstPlayerScoreIndex - 1]),
                        SecondPlayerEmail = line[model.SecondPlayerEmailIndex - 1].ToLower().Trim(),
                        SecondPlayerScore = Convert.ToInt32(line[model.SecondPlayerScoreIndex - 1]),
                        Factor =
                            model.FactorIndex.HasValue && !string.IsNullOrEmpty(line[model.FactorIndex.Value - 1])
                                ? Convert.ToDouble(line[model.FactorIndex.Value - 1])
                                : (double?) null
                    }).ToList();
                }
            }).ToList();

            var uniqueEmails =
                matches.Select(m => m.FirstPlayerEmail).Concat(matches.Select(m => m.SecondPlayerEmail)).Distinct();

            var users = await GetUsers(uniqueEmails, currentUser, league);

            foreach (var match in matches)
            {
                _context.Match.Add(new Match
                {
                    Id = Guid.NewGuid(),
                    CreatedByUser = currentUser,
                    Date = match.Date,
                    FirstPlayer = users[match.FirstPlayerEmail],
                    FirstPlayerScore = match.FirstPlayerScore,
                    SecondPlayer = users[match.SecondPlayerEmail],
                    SecondPlayerScore = match.SecondPlayerScore,
                    Factor = match.Factor,
                    League = league
                });
            }

            _context.SaveChanges();

            var leagueId = league.Id;
            return RedirectToAction(nameof(Index), new
            {
                leagueId
            });
        }

        private async Task<Dictionary<string, ApplicationUser>> GetUsers(IEnumerable<string> emails, ApplicationUser currentUser, League league)
        {
            var result = new Dictionary<string, ApplicationUser>();

            foreach (var email in emails)
            {
                result[email] = await _invitesService.Invite(email, email, string.Empty, 
                    null, null, null, null, null,
                    currentUser, league, Url);
            }

            return result;
        }
    }
}
