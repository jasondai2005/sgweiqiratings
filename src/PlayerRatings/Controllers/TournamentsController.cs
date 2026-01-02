using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayerRatings.Models;
using PlayerRatings.Util;
using PlayerRatings.ViewModels.Tournament;

namespace PlayerRatings.Controllers
{
    [Authorize]
    public class TournamentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TournamentsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Tournaments?leagueId=xxx
        public async Task<IActionResult> Index(Guid leagueId)
        {
            var currentUser = await User.GetApplicationUser(_userManager);
            
            var league = await _context.League.FindAsync(leagueId);
            if (league == null)
            {
                return NotFound();
            }

            // Check if user is in this league
            var isInLeague = await _context.LeaguePlayers
                .AnyAsync(lp => lp.LeagueId == leagueId && lp.UserId == currentUser.Id);
            if (!isInLeague)
            {
                return NotFound();
            }

            var isAdmin = league.CreatedByUserId == currentUser.Id;

            var tournaments = await _context.Tournaments
                .Where(t => t.LeagueId == leagueId)
                .OrderByDescending(t => t.StartDate ?? DateTimeOffset.MinValue)
                .ThenBy(t => t.Name)
                .Select(t => new TournamentSummary
                {
                    Id = t.Id,
                    Name = t.Name,
                    Ordinal = t.Ordinal,
                    Group = t.Group,
                    FullName = t.FullName,
                    Organizer = t.Organizer,
                    Location = t.Location,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    TournamentType = t.TournamentType,
                    Factor = t.Factor,
                    MatchCount = t.Matches.Count,
                    PlayerCount = t.TournamentPlayers.Count
                })
                .ToListAsync();

            var viewModel = new TournamentListViewModel
            {
                LeagueId = leagueId,
                LeagueName = league.Name,
                IsAdmin = isAdmin,
                Tournaments = tournaments
            };

            return View(viewModel);
        }

        // GET: Tournaments/Details/5
        public async Task<IActionResult> Details(Guid id)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .Include(t => t.Matches)
                    .ThenInclude(m => m.FirstPlayer)
                .Include(t => t.Matches)
                    .ThenInclude(m => m.SecondPlayer)
                .Include(t => t.TournamentPlayers)
                    .ThenInclude(tp => tp.Player)
                .Include(t => t.TournamentPlayers)
                    .ThenInclude(tp => tp.Promotion)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tournament == null)
            {
                return NotFound();
            }

            // Check if user is in this league
            var isInLeague = await _context.LeaguePlayers
                .AnyAsync(lp => lp.LeagueId == tournament.LeagueId && lp.UserId == currentUser.Id);
            if (!isInLeague)
            {
                return NotFound();
            }

            var isAdmin = tournament.League.CreatedByUserId == currentUser.Id;

            // Calculate player stats from matches
            var playerStats = new Dictionary<string, (int wins, int losses)>();
            foreach (var match in tournament.Matches)
            {
                if (!playerStats.ContainsKey(match.FirstPlayerId))
                    playerStats[match.FirstPlayerId] = (0, 0);
                if (!playerStats.ContainsKey(match.SecondPlayerId))
                    playerStats[match.SecondPlayerId] = (0, 0);

                if (match.FirstPlayerScore > match.SecondPlayerScore)
                {
                    var stats1 = playerStats[match.FirstPlayerId];
                    playerStats[match.FirstPlayerId] = (stats1.wins + 1, stats1.losses);
                    var stats2 = playerStats[match.SecondPlayerId];
                    playerStats[match.SecondPlayerId] = (stats2.wins, stats2.losses + 1);
                }
                else if (match.SecondPlayerScore > match.FirstPlayerScore)
                {
                    var stats1 = playerStats[match.FirstPlayerId];
                    playerStats[match.FirstPlayerId] = (stats1.wins, stats1.losses + 1);
                    var stats2 = playerStats[match.SecondPlayerId];
                    playerStats[match.SecondPlayerId] = (stats2.wins + 1, stats2.losses);
                }
            }

            var viewModel = new TournamentDetailsViewModel
            {
                Id = tournament.Id,
                LeagueId = tournament.LeagueId,
                LeagueName = tournament.League.Name,
                Name = tournament.Name,
                Ordinal = tournament.Ordinal,
                Group = tournament.Group,
                FullName = tournament.FullName,
                Organizer = tournament.Organizer,
                Location = tournament.Location,
                StartDate = tournament.StartDate,
                EndDate = tournament.EndDate,
                TournamentType = tournament.TournamentType,
                Factor = tournament.Factor,
                IsAdmin = isAdmin,
                Matches = tournament.Matches
                    .OrderBy(m => m.Date)
                    .ThenBy(m => m.Round)
                    .Select(m => new TournamentMatchViewModel
                    {
                        Id = m.Id,
                        Date = m.Date,
                        Round = m.Round,
                        FirstPlayerId = m.FirstPlayerId,
                        FirstPlayerName = m.FirstPlayer?.DisplayName,
                        SecondPlayerId = m.SecondPlayerId,
                        SecondPlayerName = m.SecondPlayer?.DisplayName,
                        FirstPlayerScore = m.FirstPlayerScore,
                        SecondPlayerScore = m.SecondPlayerScore,
                        Factor = m.Factor,
                        MatchName = m.MatchName
                    })
                    .ToList(),
                Players = tournament.TournamentPlayers
                    .OrderBy(tp => tp.Position ?? int.MaxValue)
                    .ThenBy(tp => tp.Player?.DisplayName)
                    .Select(tp => new TournamentPlayerViewModel
                    {
                        PlayerId = tp.PlayerId,
                        PlayerName = tp.Player?.DisplayName,
                        Position = tp.Position,
                        PromotionId = tp.PromotionId,
                        PromotionRanking = tp.Promotion?.Ranking,
                        MatchCount = playerStats.ContainsKey(tp.PlayerId) 
                            ? playerStats[tp.PlayerId].wins + playerStats[tp.PlayerId].losses 
                            : 0,
                        Wins = playerStats.ContainsKey(tp.PlayerId) ? playerStats[tp.PlayerId].wins : 0,
                        Losses = playerStats.ContainsKey(tp.PlayerId) ? playerStats[tp.PlayerId].losses : 0
                    })
                    .ToList()
            };

            return View(viewModel);
        }

        // GET: Tournaments/Create?leagueId=xxx
        public async Task<IActionResult> Create(Guid leagueId)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var league = await _context.League.FindAsync(leagueId);
            if (league == null || league.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            var viewModel = new TournamentEditViewModel
            {
                LeagueId = leagueId,
                LeagueName = league.Name
            };

            return View(viewModel);
        }

        // POST: Tournaments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TournamentEditViewModel model)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var league = await _context.League.FindAsync(model.LeagueId);
            if (league == null || league.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                model.LeagueName = league.Name;
                return View(model);
            }

            var tournament = new Tournament
            {
                Id = Guid.NewGuid(),
                LeagueId = model.LeagueId,
                Name = model.Name,
                Ordinal = model.Ordinal,
                Group = model.Group,
                Organizer = model.Organizer,
                Location = model.Location,
                StartDate = model.StartDate.HasValue 
                    ? new DateTimeOffset(model.StartDate.Value.Date, TimeSpan.Zero) 
                    : null,
                EndDate = model.EndDate.HasValue 
                    ? new DateTimeOffset(model.EndDate.Value.Date.AddDays(1).AddSeconds(-1), TimeSpan.Zero) 
                    : null,
                TournamentType = model.TournamentType,
                Factor = model.Factor
            };

            _context.Tournaments.Add(tournament);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Edit), new { id = tournament.Id });
        }

        // GET: Tournaments/Edit/5
        public async Task<IActionResult> Edit(Guid id, int? filterMonth, int? filterYear)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .Include(t => t.Matches)
                    .ThenInclude(m => m.FirstPlayer)
                .Include(t => t.Matches)
                    .ThenInclude(m => m.SecondPlayer)
                .Include(t => t.TournamentPlayers)
                    .ThenInclude(tp => tp.Player)
                .Include(t => t.TournamentPlayers)
                    .ThenInclude(tp => tp.Promotion)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            // Default filter to tournament dates or current month
            if (!filterYear.HasValue)
            {
                filterYear = tournament.StartDate?.Year ?? DateTimeOffset.Now.Year;
            }
            if (!filterMonth.HasValue)
            {
                filterMonth = tournament.StartDate?.Month ?? DateTimeOffset.Now.Month;
            }

            var filterStart = new DateTimeOffset(filterYear.Value, filterMonth.Value, 1, 0, 0, 0, TimeSpan.Zero);
            var filterEnd = filterStart.AddMonths(1);

            // Get available matches for selection (in the filtered month, from this league)
            var availableMatches = await _context.Match
                .Include(m => m.FirstPlayer)
                .Include(m => m.SecondPlayer)
                .Include(m => m.Tournament)
                .Where(m => m.LeagueId == tournament.LeagueId
                    && m.Date >= filterStart && m.Date < filterEnd)
                .OrderBy(m => m.Date)
                .ToListAsync();

            var selectedMatchIds = tournament.Matches.Select(m => m.Id).ToHashSet();

            var viewModel = new TournamentEditViewModel
            {
                Id = tournament.Id,
                LeagueId = tournament.LeagueId,
                LeagueName = tournament.League.Name,
                Name = tournament.Name,
                Ordinal = tournament.Ordinal,
                Group = tournament.Group,
                Organizer = tournament.Organizer,
                Location = tournament.Location,
                StartDate = tournament.StartDate?.DateTime,
                EndDate = tournament.EndDate?.DateTime,
                TournamentType = tournament.TournamentType,
                Factor = tournament.Factor,
                FilterMonth = filterMonth,
                FilterYear = filterYear,
                SelectedMatchIds = selectedMatchIds.ToList(),
                AvailableMatches = availableMatches.Select(m => new MatchSelectionItem
                {
                    Id = m.Id,
                    Date = m.Date,
                    FirstPlayerName = m.FirstPlayer?.DisplayName,
                    SecondPlayerName = m.SecondPlayer?.DisplayName,
                    FirstPlayerScore = m.FirstPlayerScore,
                    SecondPlayerScore = m.SecondPlayerScore,
                    MatchName = m.MatchName,
                    Factor = m.Factor,
                    IsSelected = selectedMatchIds.Contains(m.Id),
                    CurrentTournamentId = m.TournamentId,
                    CurrentTournamentName = m.TournamentId.HasValue && m.TournamentId != tournament.Id 
                        ? m.Tournament?.FullName 
                        : null,
                    Round = m.Round
                }).ToList(),
                SelectedPlayers = tournament.TournamentPlayers.Select(tp => new TournamentPlayerEditModel
                {
                    PlayerId = tp.PlayerId,
                    Position = tp.Position,
                    PromotionId = tp.PromotionId
                }).ToList(),
                AvailablePlayers = tournament.TournamentPlayers
                    .OrderBy(tp => tp.Position ?? int.MaxValue)
                    .ThenBy(tp => tp.Player?.DisplayName)
                    .Select(tp => new PlayerSelectionItem
                    {
                        PlayerId = tp.PlayerId,
                        PlayerName = tp.Player?.DisplayName,
                        IsSelected = true,
                        Position = tp.Position,
                        PromotionId = tp.PromotionId,
                        PromotionRanking = tp.Promotion?.Ranking
                    }).ToList()
            };

            return View(viewModel);
        }

        // POST: Tournaments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, TournamentEditViewModel model)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .Include(t => t.Matches)
                .Include(t => t.TournamentPlayers)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                model.LeagueName = tournament.League.Name;
                return View(model);
            }

            // Update basic properties
            tournament.Name = model.Name;
            tournament.Ordinal = model.Ordinal;
            tournament.Group = model.Group;
            tournament.Organizer = model.Organizer;
            tournament.Location = model.Location;
            tournament.StartDate = model.StartDate.HasValue 
                ? new DateTimeOffset(model.StartDate.Value.Date, TimeSpan.Zero) 
                : null;
            tournament.EndDate = model.EndDate.HasValue 
                ? new DateTimeOffset(model.EndDate.Value.Date.AddDays(1).AddSeconds(-1), TimeSpan.Zero) 
                : null;
            tournament.TournamentType = model.TournamentType;
            tournament.Factor = model.Factor;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Edit), new { id = tournament.Id, filterMonth = model.FilterMonth, filterYear = model.FilterYear });
        }

        // POST: Tournaments/AddMatches
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMatches(Guid tournamentId, List<Guid> matchIds, List<int?> rounds, int? filterMonth, int? filterYear)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .Include(t => t.TournamentPlayers)
                .FirstOrDefaultAsync(t => t.Id == tournamentId);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            if (matchIds == null || !matchIds.Any())
            {
                return RedirectToAction(nameof(Edit), new { id = tournamentId, filterMonth, filterYear });
            }

            var matches = await _context.Match
                .Where(m => matchIds.Contains(m.Id) && m.LeagueId == tournament.LeagueId)
                .ToListAsync();

            var existingPlayerIds = tournament.TournamentPlayers.Select(tp => tp.PlayerId).ToHashSet();
            var newPlayers = new List<TournamentPlayer>();

            DateTimeOffset? minDate = tournament.StartDate;
            DateTimeOffset? maxDate = tournament.EndDate;

            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                var round = i < rounds?.Count ? rounds[i] : null;

                // 1. Update MatchName to Organizer + Ordinal + TournamentName + Group + R{Round}
                var nameParts = new List<string>();
                if (!string.IsNullOrEmpty(tournament.Organizer))
                    nameParts.Add(tournament.Organizer);
                if (!string.IsNullOrEmpty(tournament.Ordinal))
                    nameParts.Add(tournament.Ordinal);
                nameParts.Add(tournament.Name);
                if (!string.IsNullOrEmpty(tournament.Group))
                    nameParts.Add(tournament.Group);
                if (round.HasValue)
                    nameParts.Add($"R{round.Value}");
                match.MatchName = string.Join(" ", nameParts);

                // 2. Update Match Factor if not set and Tournament has a factor
                if (!match.Factor.HasValue && tournament.Factor.HasValue)
                {
                    match.Factor = tournament.Factor;
                }

                // 3. Set TournamentId and Round
                match.TournamentId = tournament.Id;
                match.Round = round;

                // 4. Track dates for StartDate/EndDate update
                if (!minDate.HasValue || minDate == DateTimeOffset.MinValue || match.Date < minDate)
                    minDate = match.Date;
                if (!maxDate.HasValue || maxDate == DateTimeOffset.MinValue || match.Date > maxDate)
                    maxDate = match.Date;

                // 5. Add players to TournamentPlayer if not already there
                if (!existingPlayerIds.Contains(match.FirstPlayerId))
                {
                    newPlayers.Add(new TournamentPlayer
                    {
                        TournamentId = tournament.Id,
                        PlayerId = match.FirstPlayerId
                    });
                    existingPlayerIds.Add(match.FirstPlayerId);
                }
                if (!existingPlayerIds.Contains(match.SecondPlayerId))
                {
                    newPlayers.Add(new TournamentPlayer
                    {
                        TournamentId = tournament.Id,
                        PlayerId = match.SecondPlayerId
                    });
                    existingPlayerIds.Add(match.SecondPlayerId);
                }
            }

            // Update tournament dates
            if (minDate.HasValue && (!tournament.StartDate.HasValue || tournament.StartDate == DateTimeOffset.MinValue || minDate < tournament.StartDate))
            {
                tournament.StartDate = new DateTimeOffset(minDate.Value.Date, TimeSpan.Zero);
            }
            if (maxDate.HasValue && (!tournament.EndDate.HasValue || tournament.EndDate == DateTimeOffset.MinValue || maxDate > tournament.EndDate))
            {
                tournament.EndDate = new DateTimeOffset(maxDate.Value.Date.AddDays(1).AddSeconds(-1), TimeSpan.Zero);
            }

            if (newPlayers.Any())
            {
                _context.TournamentPlayers.AddRange(newPlayers);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Edit), new { id = tournamentId, filterMonth, filterYear });
        }

        // POST: Tournaments/RemoveMatch
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveMatch(Guid tournamentId, Guid matchId, int? filterMonth, int? filterYear)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .FirstOrDefaultAsync(t => t.Id == tournamentId);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            var match = await _context.Match.FindAsync(matchId);
            if (match != null && match.TournamentId == tournamentId)
            {
                match.TournamentId = null;
                match.Round = null;
                // Note: We don't reset MatchName or Factor as those may have been manually set
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Edit), new { id = tournamentId, filterMonth, filterYear });
        }

        // POST: Tournaments/UpdatePlayerPosition
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePlayerPosition(Guid tournamentId, string playerId, int? position)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .FirstOrDefaultAsync(t => t.Id == tournamentId);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            var tournamentPlayer = await _context.TournamentPlayers
                .FirstOrDefaultAsync(tp => tp.TournamentId == tournamentId && tp.PlayerId == playerId);

            if (tournamentPlayer == null)
            {
                return Json(new { success = false, message = "Player not found" });
            }

            tournamentPlayer.Position = position;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // POST: Tournaments/RemovePlayer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePlayer(Guid tournamentId, string playerId, int? filterMonth, int? filterYear)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .FirstOrDefaultAsync(t => t.Id == tournamentId);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            var tournamentPlayer = await _context.TournamentPlayers
                .FirstOrDefaultAsync(tp => tp.TournamentId == tournamentId && tp.PlayerId == playerId);

            if (tournamentPlayer != null)
            {
                _context.TournamentPlayers.Remove(tournamentPlayer);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Edit), new { id = tournamentId, filterMonth, filterYear });
        }

        // GET: Tournaments/Delete/5
        public async Task<IActionResult> Delete(Guid id)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            return View(tournament);
        }

        // POST: Tournaments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .Include(t => t.Matches)
                .Include(t => t.TournamentPlayers)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            var leagueId = tournament.LeagueId;

            // Clear tournament references from matches
            foreach (var match in tournament.Matches)
            {
                match.TournamentId = null;
                match.Round = null;
            }

            // Remove tournament players
            _context.TournamentPlayers.RemoveRange(tournament.TournamentPlayers);

            // Remove tournament
            _context.Tournaments.Remove(tournament);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { leagueId });
        }
    }
}

