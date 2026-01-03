using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayerRatings.Engine.Rating;
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

        /// <summary>
        /// Calculate Swiss-system stats for tournament players
        /// </summary>
        private static SwissStats CalculateSwissStats(IEnumerable<Match> matches)
        {
            var playerStats = new Dictionary<string, PlayerMatchStats>();
            var byeWins = new Dictionary<string, int>(); // Track bye wins separately

            foreach (var match in matches)
            {
                // Handle bye matches (opponent is NULL)
                if (string.IsNullOrEmpty(match.FirstPlayerId) || string.IsNullOrEmpty(match.SecondPlayerId))
                {
                    // Find the real player (non-null)
                    var realPlayerId = !string.IsNullOrEmpty(match.FirstPlayerId) ? match.FirstPlayerId : match.SecondPlayerId;
                    if (string.IsNullOrEmpty(realPlayerId))
                        continue; // Both players are null, skip
                    
                    if (!playerStats.ContainsKey(realPlayerId))
                        playerStats[realPlayerId] = new PlayerMatchStats();
                    
                    var realPlayerScore = !string.IsNullOrEmpty(match.FirstPlayerId) ? match.FirstPlayerScore : match.SecondPlayerScore;
                    var byeScore = !string.IsNullOrEmpty(match.FirstPlayerId) ? match.SecondPlayerScore : match.FirstPlayerScore;
                    
                    // Count bye as a win if player won, loss if player lost
                    if (realPlayerScore > byeScore)
                    {
                        playerStats[realPlayerId].Wins++;
                        byeWins[realPlayerId] = byeWins.GetValueOrDefault(realPlayerId) + 1;
                    }
                    else if (byeScore > realPlayerScore)
                    {
                        playerStats[realPlayerId].Losses++;
                    }
                    
                    // No opponent to track for bye matches, no points for/against from bye
                    continue;
                }
                
                if (!playerStats.ContainsKey(match.FirstPlayerId))
                    playerStats[match.FirstPlayerId] = new PlayerMatchStats();
                if (!playerStats.ContainsKey(match.SecondPlayerId))
                    playerStats[match.SecondPlayerId] = new PlayerMatchStats();

                var stats1 = playerStats[match.FirstPlayerId];
                var stats2 = playerStats[match.SecondPlayerId];

                // Track opponents
                stats1.Opponents.Add(match.SecondPlayerId);
                stats2.Opponents.Add(match.FirstPlayerId);

                // Update points
                stats1.PointsFor += match.FirstPlayerScore;
                stats1.PointsAgainst += match.SecondPlayerScore;
                stats2.PointsFor += match.SecondPlayerScore;
                stats2.PointsAgainst += match.FirstPlayerScore;

                // Determine winner or draw
                if (match.FirstPlayerScore > match.SecondPlayerScore)
                {
                    stats1.Wins++;
                    stats2.Losses++;
                }
                else if (match.SecondPlayerScore > match.FirstPlayerScore)
                {
                    stats1.Losses++;
                    stats2.Wins++;
                }
                else
                {
                    // Draw: award 0.5 points to each player
                    stats1.Wins += 0.5;
                    stats2.Wins += 0.5;
                    stats1.Draws++;
                    stats2.Draws++;
                }
            }

            // Calculate SOS (Sum of Opponents' Scores)
            var sosScores = new Dictionary<string, double>();
            foreach (var player in playerStats)
            {
                sosScores[player.Key] = player.Value.Opponents.Sum(oppId =>
                    playerStats.TryGetValue(oppId, out var oppStats) ? oppStats.Wins : 0);
            }

            // Calculate SOSOS (Sum of Opponents' SOS)
            var sososScores = new Dictionary<string, double>();
            foreach (var player in playerStats)
            {
                sososScores[player.Key] = player.Value.Opponents.Sum(oppId =>
                    sosScores.TryGetValue(oppId, out var oppSos) ? oppSos : 0);
            }

            return new SwissStats { PlayerStats = playerStats, SOS = sosScores, SOSOS = sososScores };
        }

        /// <summary>
        /// Calculate positions using Swiss-system ranking.
        /// All undefeated players (0 losses, at least 1 win) get position 1 (champions).
        /// Other players keep their true positions (e.g., if 3 players are at position 1, next is position 4).
        /// </summary>
        private static Dictionary<string, int> CalculateSwissPositions(SwissStats stats)
        {
            var rankedPlayers = stats.PlayerStats
                .OrderByDescending(p => p.Value.Losses == 0 && p.Value.Wins > 0 ? 1 : 0) // Undefeated first
                .ThenByDescending(p => p.Value.Wins)
                .ThenByDescending(p => stats.SOS[p.Key])
                .ThenByDescending(p => stats.SOSOS[p.Key])
                .ThenByDescending(p => p.Value.PointsFor - p.Value.PointsAgainst)
                .ToList();

            var positions = new Dictionary<string, int>();
            
            for (int i = 0; i < rankedPlayers.Count; i++)
            {
                var current = rankedPlayers[i];
                bool isUndefeated = current.Value.Losses == 0 && current.Value.Wins > 0;
                
                // All undefeated players get position 1
                if (isUndefeated)
                {
                    positions[current.Key] = 1;
                    continue;
                }
                
                // For defeated players, check for ties
                if (i > 0)
                {
                    var previous = rankedPlayers[i - 1];
                    bool previousUndefeated = previous.Value.Losses == 0 && previous.Value.Wins > 0;
                    
                    // Check for ties among defeated players (not with undefeated)
                    if (!previousUndefeated &&
                        current.Value.Wins == previous.Value.Wins &&
                        stats.SOS[current.Key] == stats.SOS[previous.Key] &&
                        stats.SOSOS[current.Key] == stats.SOSOS[previous.Key])
                    {
                        positions[current.Key] = positions[previous.Key];
                        continue;
                    }
                }
                
                // True position based on ranking order (1-indexed)
                positions[current.Key] = i + 1;
            }

            return positions;
        }

        private class PlayerMatchStats
        {
            public double Wins { get; set; }  // Use double to support draws (0.5 points each)
            public int Losses { get; set; }
            public int Draws { get; set; }
            public int PointsFor { get; set; }
            public int PointsAgainst { get; set; }
            public List<string> Opponents { get; } = new List<string>();
        }

        private class SwissStats
        {
            public Dictionary<string, PlayerMatchStats> PlayerStats { get; set; }
            public Dictionary<string, double> SOS { get; set; }  // Use double to support draws
            public Dictionary<string, double> SOSOS { get; set; }
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
                        .ThenInclude(p => p.Rankings)
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

            // Calculate Swiss-system stats
            var swissStats = CalculateSwissStats(tournament.Matches);
            var calculatedPositions = CalculateSwissPositions(swissStats);

            // Build round results for each player
            var playerRoundResults = new Dictionary<string, Dictionary<int, RoundResult>>();
            var maxRound = 0;
            
            // Get the tournament start/end dates for rating calculations
            // Use previous day of start date for "before" ratings, end date for "after" ratings
            var tournamentStartDate = (tournament.StartDate ?? tournament.Matches.Min(m => m.Date)).AddDays(-1);
            var tournamentEndDate = tournament.EndDate ?? tournament.Matches.Max(m => m.Date);
            
            // Load all league matches for rating calculation
            var leagueMatches = await _context.Match
                .Where(m => m.LeagueId == tournament.LeagueId)
                .Include(m => m.FirstPlayer).ThenInclude(p => p.Rankings)
                .Include(m => m.SecondPlayer).ThenInclude(p => p.Rankings)
                .OrderBy(m => m.Date)
                .ToListAsync();
            
            // Get tournament player IDs for focused calculation
            var tournamentPlayerIds = tournament.TournamentPlayers.Select(tp => tp.PlayerId).ToHashSet();
            
            // Player lookup function
            ApplicationUser PlayerLookup(string playerId) => 
                tournament.TournamentPlayers.FirstOrDefault(tp => tp.PlayerId == playerId)?.Player;
            
            // Determine league type for rating calculation
            bool isSgLeague = tournament.League?.Name?.Contains("Singapore Weiqi") ?? false;
            
            // Calculate ratings and ranked status before and after tournament using shared helper
            // Uses full rating calculation with match filtering and performance corrections
            // "Ranked" means the player would appear in the Ratings page at that date
            var ratingsBefore = RatingCalculationHelper.GetPlayerRatingsAndRankedStatus(
                leagueMatches, tournamentStartDate, swaOnly: false, isSgLeague, tournamentPlayerIds, PlayerLookup);
            
            var ratingsAfter = RatingCalculationHelper.GetPlayerRatingsAndRankedStatus(
                leagueMatches, tournamentEndDate, swaOnly: false, isSgLeague, tournamentPlayerIds, PlayerLookup);
            
            // Get promotion bonuses awarded during/after the tournament (not before it started)
            var promotionBonuses = RatingCalculationHelper.GetPromotionBonuses(
                leagueMatches, tournamentStartDate, tournamentEndDate, swaOnly: false, isSgLeague, tournamentPlayerIds);
            
            // Track first match date for each round (for creating new matches)
            var roundDates = new Dictionary<int, DateTimeOffset>();
            
            foreach (var match in tournament.Matches.Where(m => m.Round.HasValue))
            {
                var round = match.Round.Value;
                if (round > maxRound) maxRound = round;
                
                // Track first match date for this round
                if (!roundDates.ContainsKey(round) || match.Date < roundDates[round])
                    roundDates[round] = match.Date;
                
                // Determine winner
                bool? firstWon = null;
                if (match.FirstPlayerScore > match.SecondPlayerScore)
                    firstWon = true;
                else if (match.SecondPlayerScore > match.FirstPlayerScore)
                    firstWon = false;
                
                // Handle bye matches (one player is NULL)
                if (string.IsNullOrEmpty(match.FirstPlayerId) || string.IsNullOrEmpty(match.SecondPlayerId))
                {
                    // Find the real player (non-null)
                    var realPlayerId = !string.IsNullOrEmpty(match.FirstPlayerId) ? match.FirstPlayerId : match.SecondPlayerId;
                    if (string.IsNullOrEmpty(realPlayerId))
                        continue; // Both players are null, skip
                    
                    if (!playerRoundResults.ContainsKey(realPlayerId))
                        playerRoundResults[realPlayerId] = new Dictionary<int, RoundResult>();
                    
                    var realPlayerWon = !string.IsNullOrEmpty(match.FirstPlayerId) ? firstWon : (firstWon.HasValue ? !firstWon.Value : null);
                    var realPlayerScore = !string.IsNullOrEmpty(match.FirstPlayerId) 
                        ? $"{match.FirstPlayerScore}:{match.SecondPlayerScore}"
                        : $"{match.SecondPlayerScore}:{match.FirstPlayerScore}";
                    
                    playerRoundResults[realPlayerId][round] = new RoundResult
                    {
                        OpponentId = null,
                        OpponentName = "BYE",
                        Won = realPlayerWon,
                        Score = realPlayerScore
                    };
                    continue;
                }
                
                // Initialize dictionaries if needed
                if (!playerRoundResults.ContainsKey(match.FirstPlayerId))
                    playerRoundResults[match.FirstPlayerId] = new Dictionary<int, RoundResult>();
                if (!playerRoundResults.ContainsKey(match.SecondPlayerId))
                    playerRoundResults[match.SecondPlayerId] = new Dictionary<int, RoundResult>();
                
                // Add results for first player
                playerRoundResults[match.FirstPlayerId][round] = new RoundResult
                {
                    OpponentId = match.SecondPlayerId,
                    OpponentName = match.SecondPlayer?.DisplayName,
                    Won = firstWon,
                    Score = $"{match.FirstPlayerScore}:{match.SecondPlayerScore}"
                };
                
                // Add results for second player
                playerRoundResults[match.SecondPlayerId][round] = new RoundResult
                {
                    OpponentId = match.FirstPlayerId,
                    OpponentName = match.FirstPlayer?.DisplayName,
                    Won = firstWon.HasValue ? !firstWon.Value : null,
                    Score = $"{match.SecondPlayerScore}:{match.FirstPlayerScore}"
                };
            }
            
            // Build player lookup for display names
            var playerLookup = tournament.TournamentPlayers.ToDictionary(tp => tp.PlayerId, tp => tp.Player);
            
            // Run ELO calculation on league matches to populate OldFirstPlayerRating, OldSecondPlayerRating, ShiftRating
            // This is the same calculation done in the Ratings page
            RatingCalculationHelper.CalculateRatings(leagueMatches, tournamentEndDate, swaOnly: false, isSgLeague);
            
            // Get tournament matches from the calculated league matches (same objects with populated ratings)
            var tournamentMatchIds = tournament.Matches.Select(m => m.Id).ToHashSet();
            var orderedMatches = leagueMatches
                .Where(m => tournamentMatchIds.Contains(m.Id))
                .OrderBy(m => m.Date)
                .ThenBy(m => m.Round)
                .ToList();

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
                MaxRounds = maxRound,
                RoundDates = roundDates,
                Matches = orderedMatches
                    .Select(m => new TournamentMatchViewModel
                    {
                        Id = m.Id,
                        Date = m.Date,
                        Round = m.Round,
                        FirstPlayerId = m.FirstPlayerId,
                        FirstPlayerName = m.FirstPlayer?.DisplayName,
                        FirstPlayerRanking = m.FirstPlayer?.GetCombinedRankingBeforeDate(m.Date),
                        SecondPlayerId = m.SecondPlayerId,
                        SecondPlayerName = m.SecondPlayer?.DisplayName,
                        SecondPlayerRanking = m.SecondPlayer?.GetCombinedRankingBeforeDate(m.Date),
                        FirstPlayerScore = m.FirstPlayerScore,
                        SecondPlayerScore = m.SecondPlayerScore,
                        Factor = m.Factor,
                        MatchName = m.MatchName,
                        // Use ratings from ELO calculation (same as Ratings page)
                        FirstPlayerRatingBefore = m.OldFirstPlayerRating,
                        SecondPlayerRatingBefore = m.OldSecondPlayerRating,
                        ShiftRating = m.ShiftRating
                    })
                    .ToList(),
                Players = tournament.TournamentPlayers
                    .Select(tp => {
                        var hasStats = swissStats.PlayerStats.TryGetValue(tp.PlayerId, out var pStats);
                        return new TournamentPlayerViewModel
                        {
                            PlayerId = tp.PlayerId,
                            PlayerName = tp.Player?.DisplayName,
                            PlayerRanking = tp.Player?.GetCombinedRankingBeforeDate(tournamentStartDate),
                            Residence = tp.Player?.GetResidenceAt(tournamentStartDate),
                            Position = tp.Position,
                            CalculatedPosition = calculatedPositions.TryGetValue(tp.PlayerId, out var calcPos) ? calcPos : 0,
                            PromotionId = tp.PromotionId,
                            PromotionRanking = tp.Promotion?.Ranking,
                            PromotionBonus = promotionBonuses.TryGetValue(tp.PlayerId, out var bonus) ? bonus : null,
                            MatchCount = hasStats ? pStats.Opponents.Count : 0,
                            Wins = hasStats ? pStats.Wins : 0,
                            Losses = hasStats ? pStats.Losses : 0,
                            PointDiff = hasStats ? pStats.PointsFor - pStats.PointsAgainst : 0,
                            SOS = swissStats.SOS.TryGetValue(tp.PlayerId, out var sos) ? sos : 0,
                            SOSOS = swissStats.SOSOS.TryGetValue(tp.PlayerId, out var sosos) ? sosos : 0,
                            RoundResults = playerRoundResults.TryGetValue(tp.PlayerId, out var rounds) ? rounds : new Dictionary<int, RoundResult>(),
                            RatingBefore = ratingsBefore.TryGetValue(tp.PlayerId, out var rBefore) ? rBefore.rating : null,
                            RatingAfter = ratingsAfter.TryGetValue(tp.PlayerId, out var rAfter) ? rAfter.rating : null,
                            // Check if player was "ranked" (shown in ratings page) before and after tournament
                            WasRankedBefore = ratingsBefore.TryGetValue(tp.PlayerId, out var beforeStatus) && beforeStatus.isRanked,
                            IsRankedAfter = ratingsAfter.TryGetValue(tp.PlayerId, out var afterStatus) && afterStatus.isRanked
                        };
                    })
                    .OrderBy(p => p.DisplayPosition)
                    .ThenByDescending(p => p.Wins)
                    .ThenBy(p => p.PlayerName)
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
        public async Task<IActionResult> Edit(Guid id, int? filterMonth, int? filterYear, string filterMatchName)
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
            var matchQuery = _context.Match
                .Include(m => m.FirstPlayer)
                .Include(m => m.SecondPlayer)
                .Include(m => m.Tournament)
                .Where(m => m.LeagueId == tournament.LeagueId
                    && m.Date >= filterStart && m.Date < filterEnd);
            
            // Apply match name filter if provided
            if (!string.IsNullOrWhiteSpace(filterMatchName))
            {
                matchQuery = matchQuery.Where(m => m.MatchName != null && m.MatchName.Contains(filterMatchName));
            }
            
            var availableMatches = await matchQuery
                .OrderBy(m => m.Date)
                .ToListAsync();

            var selectedMatchIds = tournament.Matches.Select(m => m.Id).ToHashSet();

            // Calculate Swiss-system stats
            var swissStats = CalculateSwissStats(tournament.Matches);

            // Get league players not already in tournament
            var existingPlayerIds = tournament.TournamentPlayers.Select(tp => tp.PlayerId).ToHashSet();
            var leaguePlayers = await _context.LeaguePlayers
                .Include(lp => lp.User)
                .Where(lp => lp.LeagueId == tournament.LeagueId && !existingPlayerIds.Contains(lp.UserId))
                .OrderBy(lp => lp.User.DisplayName)
                .Select(lp => new LeaguePlayerItem
                {
                    PlayerId = lp.UserId,
                    PlayerName = lp.User.DisplayName
                })
                .ToListAsync();

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
                FilterMatchName = filterMatchName,
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
                    .Select(tp => {
                        var hasStats = swissStats.PlayerStats.TryGetValue(tp.PlayerId, out var pStats);
                        return new PlayerSelectionItem
                        {
                            PlayerId = tp.PlayerId,
                            PlayerName = tp.Player?.DisplayName,
                            IsSelected = true,
                            Position = tp.Position,
                            PromotionId = tp.PromotionId,
                            PromotionRanking = tp.Promotion?.Ranking,
                            Wins = hasStats ? pStats.Wins : 0,
                            Losses = hasStats ? pStats.Losses : 0,
                            SOS = swissStats.SOS.TryGetValue(tp.PlayerId, out var sos) ? sos : 0,
                            SOSOS = swissStats.SOSOS.TryGetValue(tp.PlayerId, out var sosos) ? sosos : 0
                        };
                    }).ToList(),
                LeaguePlayers = leaguePlayers
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
        public async Task<IActionResult> AddMatches(Guid tournamentId, List<Guid> matchIds, List<int?> rounds, int? filterMonth, int? filterYear, string filterMatchName)
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
                return RedirectToAction(nameof(Edit), new { id = tournamentId, filterMonth, filterYear, filterMatchName });
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

            return RedirectToAction(nameof(Edit), new { id = tournamentId, filterMonth, filterYear, filterMatchName });
        }

        // POST: Tournaments/SaveRounds
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRounds(Guid tournamentId, List<Guid> matchIds, List<int?> rounds, int? filterMonth, int? filterYear, string filterMatchName)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .FirstOrDefaultAsync(t => t.Id == tournamentId);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            if (matchIds == null || !matchIds.Any())
            {
                return RedirectToAction(nameof(Edit), new { id = tournamentId, filterMonth, filterYear, filterMatchName });
            }

            // Only update matches that are already in this tournament
            var matches = await _context.Match
                .Where(m => matchIds.Contains(m.Id) && m.TournamentId == tournamentId)
                .ToListAsync();

            for (int i = 0; i < matchIds.Count; i++)
            {
                var matchId = matchIds[i];
                var round = i < rounds?.Count ? rounds[i] : null;
                var match = matches.FirstOrDefault(m => m.Id == matchId);
                
                if (match != null)
                {
                    match.Round = round;
                    
                    // Update MatchName to include round
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
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Edit), new { id = tournamentId, filterMonth, filterYear, filterMatchName });
        }

        // POST: Tournaments/ShiftMatchTimes - shift selected match times by X hours
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ShiftMatchTimes(Guid tournamentId, List<Guid> matchIds, int hours, int? filterMonth, int? filterYear, string filterMatchName)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .FirstOrDefaultAsync(t => t.Id == tournamentId);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            if (hours == 0 || matchIds == null || !matchIds.Any())
            {
                return RedirectToAction(nameof(Edit), new { id = tournamentId, filterMonth, filterYear, filterMatchName });
            }

            // Load and shift selected matches
            var matches = await _context.Match
                .Where(m => matchIds.Contains(m.Id) && m.TournamentId == tournamentId)
                .ToListAsync();

            foreach (var match in matches)
            {
                match.Date = match.Date.AddHours(hours);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Edit), new { id = tournamentId, filterMonth, filterYear, filterMatchName });
        }

        // POST: Tournaments/RemoveMatch
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveMatch(Guid tournamentId, Guid matchId, int? filterMonth, int? filterYear, string filterMatchName)
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

            return RedirectToAction(nameof(Edit), new { id = tournamentId, filterMonth, filterYear, filterMatchName });
        }

        // POST: Tournaments/RemovePlayer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePlayer(Guid tournamentId, string playerId, int? filterMonth, int? filterYear, string filterMatchName)
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

            return RedirectToAction(nameof(Edit), new { id = tournamentId, filterMonth, filterYear, filterMatchName });
        }

        // POST: Tournaments/AddPlayer - manually add a player to tournament
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPlayer(Guid tournamentId, string playerId, int? filterMonth, int? filterYear, string filterMatchName)
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

            // Check if player is in the league
            var isInLeague = await _context.LeaguePlayers
                .AnyAsync(lp => lp.LeagueId == tournament.LeagueId && lp.UserId == playerId);

            if (!isInLeague)
            {
                return NotFound();
            }

            // Check if player is already in tournament
            if (!tournament.TournamentPlayers.Any(tp => tp.PlayerId == playerId))
            {
                _context.TournamentPlayers.Add(new TournamentPlayer
                {
                    TournamentId = tournamentId,
                    PlayerId = playerId
                });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Edit), new { id = tournamentId, filterMonth, filterYear, filterMatchName });
        }

        // POST: Tournaments/SavePositions - save all positions at once
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePositions(Guid tournamentId, List<string> playerIds, List<int?> positionValues, int? filterMonth, int? filterYear, string filterMatchName)
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

            if (playerIds != null && positionValues != null)
            {
                for (int i = 0; i < playerIds.Count && i < positionValues.Count; i++)
                {
                    var tournamentPlayer = tournament.TournamentPlayers.FirstOrDefault(tp => tp.PlayerId == playerIds[i]);
                    if (tournamentPlayer != null)
                    {
                        tournamentPlayer.Position = positionValues[i];
                    }
                }
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Edit), new { id = tournamentId, filterMonth, filterYear, filterMatchName });
        }

        // POST: Tournaments/CalculatePositions
        // Uses Swiss-system: undefeated players are champions, then rank by wins → SOS → SOSOS
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CalculatePositions(Guid tournamentId, int? filterMonth, int? filterYear, string filterMatchName)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .Include(t => t.Matches)
                .Include(t => t.TournamentPlayers)
                .FirstOrDefaultAsync(t => t.Id == tournamentId);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            // Calculate positions using Swiss-system
            var swissStats = CalculateSwissStats(tournament.Matches);
            var positions = CalculateSwissPositions(swissStats);

            // Update positions in database
            foreach (var kvp in positions)
            {
                var tournamentPlayer = tournament.TournamentPlayers.FirstOrDefault(tp => tp.PlayerId == kvp.Key);
                if (tournamentPlayer != null)
                {
                    tournamentPlayer.Position = kvp.Value;
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Edit), new { id = tournamentId, filterMonth, filterYear, filterMatchName });
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

