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

        /// <summary>
        /// Calculate team standings based on tournament type:
        /// - If SupportsPersonalAward: sum of top 3 players' positions per team
        /// - If not SupportsPersonalAward: Swiss-system ranking based on team results (3+ wins = team win, 2 wins = draw, etc.)
        /// </summary>
        private List<TeamStandingViewModel> CalculateTeamStandings(
            Tournament tournament, 
            SwissStats playerSwissStats, 
            Dictionary<string, int> playerPositions,
            Dictionary<string, Dictionary<int, RoundResult>> playerRoundResults,
            int maxRound)
        {
            // Group players by team
            var playersByTeam = tournament.TournamentPlayers
                .Where(tp => !string.IsNullOrEmpty(tp.Team))
                .GroupBy(tp => tp.Team)
                .ToDictionary(g => g.Key, g => g.ToList());
            
            if (!playersByTeam.Any())
                return new List<TeamStandingViewModel>();

            var teamStandings = new List<TeamStandingViewModel>();
            
            if (tournament.SupportsPersonalAward)
            {
                // Personal award mode: rank teams by sum of top 3 players' positions
                foreach (var teamGroup in playersByTeam)
                {
                    var teamName = teamGroup.Key;
                    var players = teamGroup.Value
                        .OrderBy(tp => tp.Position ?? playerPositions.GetValueOrDefault(tp.PlayerId, int.MaxValue))
                        .ToList();
                    
                    // Get top 3 (or less if team has fewer players)
                    var countingPlayers = players.Take(3).ToList();
                    var hasStats = playerSwissStats.PlayerStats;
                    
                    // Calculate sum of positions
                    int sumOfPositions = 0;
                    double totalWins = 0;
                    
                    // If team has fewer than 3 players, add penalty (number of all players)
                    int playerPenalty = countingPlayers.Count < 3 
                        ? (3 - countingPlayers.Count) * tournament.TournamentPlayers.Count 
                        : 0;
                    
                    var teamPlayers = new List<TeamPlayerViewModel>();
                    foreach (var tp in players)
                    {
                        var pos = tp.Position ?? playerPositions.GetValueOrDefault(tp.PlayerId, 0);
                        var countsForAward = countingPlayers.Contains(tp);
                        
                        if (countsForAward)
                            sumOfPositions += pos;
                        
                        if (hasStats.TryGetValue(tp.PlayerId, out var pStats))
                            totalWins += pStats.Wins;
                        
                        teamPlayers.Add(new TeamPlayerViewModel
                        {
                            PlayerId = tp.PlayerId,
                            PlayerName = tp.Player?.DisplayName,
                            PersonalPosition = pos,
                            CountsForTeamAward = countsForAward,
                            RoundResults = playerRoundResults.TryGetValue(tp.PlayerId, out var rounds) 
                                ? rounds 
                                : new Dictionary<int, RoundResult>()
                        });
                    }
                    
                    sumOfPositions += playerPenalty;
                    
                    teamStandings.Add(new TeamStandingViewModel
                    {
                        TeamName = teamName,
                        Players = teamPlayers,
                        TotalPlayerWins = totalWins,
                        SumOfPlayerPositions = sumOfPositions
                    });
                }
                
                // Rank by sum of positions (lower is better)
                var ranked = teamStandings
                    .OrderBy(t => t.SumOfPlayerPositions)
                    .ThenByDescending(t => t.TotalPlayerWins)
                    .ToList();
                
                for (int i = 0; i < ranked.Count; i++)
                {
                    ranked[i].Index = i + 1;
                    ranked[i].Position = i + 1;
                    
                    // Handle ties
                    if (i > 0 && ranked[i].SumOfPlayerPositions == ranked[i - 1].SumOfPlayerPositions 
                              && ranked[i].TotalPlayerWins == ranked[i - 1].TotalPlayerWins)
                    {
                        ranked[i].Position = ranked[i - 1].Position;
                    }
                }
                
                return ranked;
            }
            else
            {
                // Team mode: Swiss-system based on team results per round
                // Assuming teams have 4 players: 3 wins = team win, 2 wins = draw, 1 or 0 wins = loss
                // 2 wins 1 draw = win
                
                // Build team round results
                var teamRoundWins = new Dictionary<string, Dictionary<int, (double wins, double losses, double draws, string opponentTeam)>>();
                
                // For each round, determine team results based on player matchups
                for (int round = 1; round <= maxRound; round++)
                {
                    // Get all matches in this round
                    var roundMatches = tournament.Matches.Where(m => m.Round == round).ToList();
                    
                    // Track wins per team in this round
                    var teamWinsInRound = new Dictionary<string, double>();
                    var teamOpponents = new Dictionary<string, HashSet<string>>();
                    
                    foreach (var match in roundMatches)
                    {
                        var player1Team = tournament.TournamentPlayers.FirstOrDefault(tp => tp.PlayerId == match.FirstPlayerId)?.Team;
                        var player2Team = tournament.TournamentPlayers.FirstOrDefault(tp => tp.PlayerId == match.SecondPlayerId)?.Team;
                        
                        if (string.IsNullOrEmpty(player1Team) || string.IsNullOrEmpty(player2Team))
                            continue;
                        
                        // Track opponents
                        if (!teamOpponents.ContainsKey(player1Team))
                            teamOpponents[player1Team] = new HashSet<string>();
                        if (!teamOpponents.ContainsKey(player2Team))
                            teamOpponents[player2Team] = new HashSet<string>();
                        teamOpponents[player1Team].Add(player2Team);
                        teamOpponents[player2Team].Add(player1Team);
                        
                        // Initialize wins
                        if (!teamWinsInRound.ContainsKey(player1Team))
                            teamWinsInRound[player1Team] = 0;
                        if (!teamWinsInRound.ContainsKey(player2Team))
                            teamWinsInRound[player2Team] = 0;
                        
                        // Determine winner
                        if (match.FirstPlayerScore > match.SecondPlayerScore)
                            teamWinsInRound[player1Team]++;
                        else if (match.SecondPlayerScore > match.FirstPlayerScore)
                            teamWinsInRound[player2Team]++;
                        else
                        {
                            // Draw: 0.5 each
                            teamWinsInRound[player1Team] += 0.5;
                            teamWinsInRound[player2Team] += 0.5;
                        }
                    }
                    
                    // Now determine team vs team results
                    foreach (var team in teamWinsInRound.Keys)
                    {
                        if (!teamRoundWins.ContainsKey(team))
                            teamRoundWins[team] = new Dictionary<int, (double, double, double, string)>();
                        
                        var wins = teamWinsInRound[team];
                        var opponents = teamOpponents.GetValueOrDefault(team, new HashSet<string>());
                        var opponentTeam = opponents.FirstOrDefault() ?? "";
                        
                        // Determine team result: 3+ wins = win, 2 wins = draw, <2 wins = loss
                        // For 4 player teams: 2 wins 1 draw = 2.5 = win
                        double teamWin = 0, teamLoss = 0, teamDraw = 0;
                        if (wins >= 2.5)
                            teamWin = 1;
                        else if (wins >= 2)
                            teamDraw = 1;
                        else
                            teamLoss = 1;
                        
                        teamRoundWins[team][round] = (teamWin, teamLoss, teamDraw, opponentTeam);
                    }
                }
                
                // Now calculate team swiss stats
                var teamStats = new Dictionary<string, (double wins, int losses, int draws)>();
                foreach (var team in playersByTeam.Keys)
                {
                    double totalWins = 0;
                    int totalLosses = 0;
                    int totalDraws = 0;
                    
                    if (teamRoundWins.TryGetValue(team, out var rounds))
                    {
                        foreach (var r in rounds.Values)
                        {
                            totalWins += r.wins;
                            if (r.losses > 0) totalLosses++;
                            if (r.draws > 0) totalDraws++;
                        }
                    }
                    
                    teamStats[team] = (totalWins, totalLosses, totalDraws);
                }
                
                // Calculate team SOS
                var teamSOS = new Dictionary<string, double>();
                var teamOpponentsList = new Dictionary<string, List<string>>();
                foreach (var team in playersByTeam.Keys)
                {
                    var opponents = new List<string>();
                    if (teamRoundWins.TryGetValue(team, out var rounds))
                    {
                        foreach (var r in rounds.Values)
                        {
                            if (!string.IsNullOrEmpty(r.opponentTeam))
                                opponents.Add(r.opponentTeam);
                        }
                    }
                    teamOpponentsList[team] = opponents;
                    teamSOS[team] = opponents.Sum(opp => teamStats.TryGetValue(opp, out var oppStats) ? oppStats.wins : 0);
                }
                
                // Calculate team SOSOS
                var teamSOSOS = new Dictionary<string, double>();
                foreach (var team in playersByTeam.Keys)
                {
                    var opponents = teamOpponentsList.GetValueOrDefault(team, new List<string>());
                    teamSOSOS[team] = opponents.Sum(opp => teamSOS.GetValueOrDefault(opp, 0));
                }
                
                // Build team standings
                foreach (var teamGroup in playersByTeam)
                {
                    var teamName = teamGroup.Key;
                    var hasStats = playerSwissStats.PlayerStats;
                    double totalPlayerWins = 0;
                    
                    var teamPlayers = teamGroup.Value.Select(tp => {
                        if (hasStats.TryGetValue(tp.PlayerId, out var pStats))
                            totalPlayerWins += pStats.Wins;
                        
                        return new TeamPlayerViewModel
                        {
                            PlayerId = tp.PlayerId,
                            PlayerName = tp.Player?.DisplayName,
                            PersonalPosition = null, // Not used in team mode
                            CountsForTeamAward = true, // All players count in team mode
                            RoundResults = playerRoundResults.TryGetValue(tp.PlayerId, out var rounds) 
                                ? rounds 
                                : new Dictionary<int, RoundResult>()
                        };
                    }).ToList();
                    
                    // Build team round results
                    var teamRoundResults = new Dictionary<int, TeamRoundResult>();
                    if (teamRoundWins.TryGetValue(teamName, out var roundWins))
                    {
                        foreach (var kvp in roundWins)
                        {
                            var round = kvp.Key;
                            var (wins, losses, draws, oppTeam) = kvp.Value;
                            bool? won = wins > 0 ? true : (draws > 0 ? (bool?)null : false);
                            
                            teamRoundResults[round] = new TeamRoundResult
                            {
                                OpponentTeamName = oppTeam,
                                OpponentTeamIndex = 0, // Will be set after ranking
                                Won = won,
                                Score = $"{wins}:{losses}"
                            };
                        }
                    }
                    
                    var stats = teamStats.GetValueOrDefault(teamName, (0, 0, 0));
                    teamStandings.Add(new TeamStandingViewModel
                    {
                        TeamName = teamName,
                        Players = teamPlayers,
                        TeamWins = stats.wins,
                        TeamSOS = teamSOS.GetValueOrDefault(teamName, 0),
                        TeamSOSOS = teamSOSOS.GetValueOrDefault(teamName, 0),
                        TotalPlayerWins = totalPlayerWins,
                        TeamRoundResults = teamRoundResults
                    });
                }
                
                // Rank teams by Swiss system
                var ranked = teamStandings
                    .OrderByDescending(t => t.TeamWins)
                    .ThenByDescending(t => t.TeamSOS)
                    .ThenByDescending(t => t.TeamSOSOS)
                    .ThenByDescending(t => t.TotalPlayerWins)
                    .ToList();
                
                // Assign indices and positions
                var teamIndexLookup = new Dictionary<string, int>();
                for (int i = 0; i < ranked.Count; i++)
                {
                    ranked[i].Index = i + 1;
                    ranked[i].Position = i + 1;
                    teamIndexLookup[ranked[i].TeamName] = i + 1;
                    
                    // Handle ties
                    if (i > 0 && ranked[i].TeamWins == ranked[i - 1].TeamWins 
                              && ranked[i].TeamSOS == ranked[i - 1].TeamSOS
                              && ranked[i].TeamSOSOS == ranked[i - 1].TeamSOSOS)
                    {
                        ranked[i].Position = ranked[i - 1].Position;
                    }
                }
                
                // Update opponent team indices
                foreach (var team in ranked)
                {
                    foreach (var roundResult in team.TeamRoundResults.Values)
                    {
                        if (!string.IsNullOrEmpty(roundResult.OpponentTeamName))
                        {
                            roundResult.OpponentTeamIndex = teamIndexLookup.GetValueOrDefault(roundResult.OpponentTeamName, 0);
                        }
                    }
                }
                
                return ranked;
            }
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

            // Calculate female positions if SupportsFemaleAward is enabled
            // Female positions are derived from personal positions (not separate Swiss ranking)
            var femalePositions = new Dictionary<string, int>();
            if (tournament.SupportsFemaleAward && tournament.SupportsPersonalAward)
            {
                var femalePlayers = tournament.TournamentPlayers
                    .Where(tp => tp.Player?.DisplayName?.Contains("♀") == true)
                    .OrderBy(tp => tp.Position ?? calculatedPositions.GetValueOrDefault(tp.PlayerId, int.MaxValue))
                    .ThenByDescending(tp => swissStats.PlayerStats.TryGetValue(tp.PlayerId, out var s) ? s.Wins : 0)
                    .ToList();
                
                int femaleRank = 1;
                for (int i = 0; i < femalePlayers.Count; i++)
                {
                    var player = femalePlayers[i];
                    var playerPos = player.Position ?? calculatedPositions.GetValueOrDefault(player.PlayerId, int.MaxValue);
                    
                    // Handle ties - if same personal position, give same female position
                    if (i > 0)
                    {
                        var prevPlayer = femalePlayers[i - 1];
                        var prevPos = prevPlayer.Position ?? calculatedPositions.GetValueOrDefault(prevPlayer.PlayerId, int.MaxValue);
                        if (playerPos != prevPos)
                        {
                            femaleRank = i + 1;
                        }
                    }
                    
                    femalePositions[player.PlayerId] = femaleRank;
                }
            }
            
            // Calculate team standings if SupportsTeamAward is enabled
            var teamStandings = new List<TeamStandingViewModel>();
            if (tournament.SupportsTeamAward)
            {
                teamStandings = CalculateTeamStandings(
                    tournament, 
                    swissStats, 
                    calculatedPositions, 
                    playerRoundResults, 
                    maxRound);
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
                Notes = tournament.Notes,
                ExternalLinks = tournament.ExternalLinks,
                Photo = tournament.Photo,
                StandingsPhoto = tournament.StandingsPhoto,
                SupportsPersonalAward = tournament.SupportsPersonalAward,
                SupportsTeamAward = tournament.SupportsTeamAward,
                SupportsFemaleAward = tournament.SupportsFemaleAward,
                IsAdmin = isAdmin,
                MaxRounds = maxRound,
                RoundDates = roundDates,
                TeamStandings = teamStandings,
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
                        var isFemale = tp.Player?.DisplayName?.Contains("♀") == true;
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
                            IsRankedAfter = ratingsAfter.TryGetValue(tp.PlayerId, out var afterStatus) && afterStatus.isRanked,
                            // Team and female-specific fields
                            Team = tp.Team,
                            TeamPosition = tp.TeamPosition,
                            FemalePosition = tp.FemalePosition ?? (femalePositions.TryGetValue(tp.PlayerId, out var femPos) ? femPos : (int?)null),
                            IsFemale = isFemale
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
                Factor = model.Factor,
                Notes = model.Notes,
                ExternalLinks = model.ExternalLinks,
                Photo = model.Photo,
                StandingsPhoto = model.StandingsPhoto,
                SupportsPersonalAward = model.SupportsPersonalAward,
                SupportsTeamAward = model.SupportsTeamAward,
                // SupportsFemaleAward only valid if SupportsPersonalAward is true
                SupportsFemaleAward = model.SupportsPersonalAward && model.SupportsFemaleAward
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
                Notes = tournament.Notes,
                ExternalLinks = tournament.ExternalLinks,
                Photo = tournament.Photo,
                StandingsPhoto = tournament.StandingsPhoto,
                SupportsPersonalAward = tournament.SupportsPersonalAward,
                SupportsTeamAward = tournament.SupportsTeamAward,
                SupportsFemaleAward = tournament.SupportsFemaleAward,
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
                            SOSOS = swissStats.SOSOS.TryGetValue(tp.PlayerId, out var sosos) ? sosos : 0,
                            Team = tp.Team,
                            TeamPosition = tp.TeamPosition,
                            FemalePosition = tp.FemalePosition,
                            IsFemale = tp.Player?.DisplayName?.Contains("♀") == true
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
            tournament.Notes = model.Notes;
            tournament.ExternalLinks = model.ExternalLinks;
            tournament.Photo = model.Photo;
            tournament.StandingsPhoto = model.StandingsPhoto;
            tournament.SupportsPersonalAward = model.SupportsPersonalAward;
            tournament.SupportsTeamAward = model.SupportsTeamAward;
            // SupportsFemaleAward only valid if SupportsPersonalAward is true
            tournament.SupportsFemaleAward = model.SupportsPersonalAward && model.SupportsFemaleAward;

            // Save player data if provided
            if (model.PlayerIds != null && model.PlayerIds.Count > 0)
            {
                for (int i = 0; i < model.PlayerIds.Count; i++)
                {
                    var tournamentPlayer = tournament.TournamentPlayers.FirstOrDefault(tp => tp.PlayerId == model.PlayerIds[i]);
                    if (tournamentPlayer != null)
                    {
                        if (model.PositionValues != null && i < model.PositionValues.Count)
                            tournamentPlayer.Position = model.PositionValues[i];
                        
                        if (model.TeamValues != null && i < model.TeamValues.Count)
                            tournamentPlayer.Team = string.IsNullOrWhiteSpace(model.TeamValues[i]) ? null : model.TeamValues[i].Trim();
                        
                        if (model.TeamPositionValues != null && i < model.TeamPositionValues.Count)
                            tournamentPlayer.TeamPosition = model.TeamPositionValues[i];
                        
                        if (model.FemalePositionValues != null && i < model.FemalePositionValues.Count)
                            tournamentPlayer.FemalePosition = model.FemalePositionValues[i];
                    }
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Edit), new { id = tournament.Id, filterMonth = model.FilterMonth, filterYear = model.FilterYear, filterMatchName = model.FilterMatchName });
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
                    .ThenInclude(tp => tp.Player)
                .FirstOrDefaultAsync(t => t.Id == tournamentId);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            // Calculate personal positions using Swiss-system
            var swissStats = CalculateSwissStats(tournament.Matches);
            var positions = CalculateSwissPositions(swissStats);

            // Update personal positions in database
            foreach (var kvp in positions)
            {
                var tournamentPlayer = tournament.TournamentPlayers.FirstOrDefault(tp => tp.PlayerId == kvp.Key);
                if (tournamentPlayer != null)
                {
                    tournamentPlayer.Position = kvp.Value;
                }
            }

            // Calculate female positions (only if SupportsFemaleAward is true)
            if (tournament.SupportsFemaleAward)
            {
                // Female players are ranked by their personal position order
                var femalePlayers = tournament.TournamentPlayers
                    .Where(tp => tp.Player?.DisplayName?.Contains("♀") == true)
                    .OrderBy(tp => tp.Position ?? positions.GetValueOrDefault(tp.PlayerId, int.MaxValue))
                    .ThenByDescending(tp => swissStats.PlayerStats.TryGetValue(tp.PlayerId, out var s) ? s.Wins : 0)
                    .ToList();

                int femaleRank = 1;
                for (int i = 0; i < femalePlayers.Count; i++)
                {
                    var player = femalePlayers[i];
                    var playerPos = player.Position ?? positions.GetValueOrDefault(player.PlayerId, int.MaxValue);

                    // Handle ties - if same personal position, give same female position
                    if (i > 0)
                    {
                        var prevPlayer = femalePlayers[i - 1];
                        var prevPos = prevPlayer.Position ?? positions.GetValueOrDefault(prevPlayer.PlayerId, int.MaxValue);
                        if (playerPos != prevPos)
                        {
                            femaleRank = i + 1;
                        }
                    }

                    player.FemalePosition = femaleRank;
                }
            }

            // Calculate team positions (only if SupportsTeamAward is true)
            if (tournament.SupportsTeamAward)
            {
                var playersByTeam = tournament.TournamentPlayers
                    .Where(tp => !string.IsNullOrEmpty(tp.Team))
                    .GroupBy(tp => tp.Team)
                    .ToDictionary(g => g.Key, g => g.ToList());

                if (playersByTeam.Any())
                {
                    if (tournament.SupportsPersonalAward)
                    {
                        // Personal award mode: rank teams by sum of top 3 players' positions
                        // Teams with only 1 player are not qualified
                        var teamScores = new List<(string TeamName, int SumPositions, double TotalWins, List<TournamentPlayer> CountingPlayers)>();

                        foreach (var teamGroup in playersByTeam)
                        {
                            var teamName = teamGroup.Key;
                            var players = teamGroup.Value
                                .OrderBy(tp => tp.Position ?? positions.GetValueOrDefault(tp.PlayerId, int.MaxValue))
                                .ToList();

                            // Skip teams with only 1 player - not qualified
                            if (players.Count < 2)
                                continue;

                            var countingPlayers = players.Take(3).ToList();
                            int sumOfPositions = countingPlayers.Sum(tp => tp.Position ?? positions.GetValueOrDefault(tp.PlayerId, 0));

                            // Penalty for teams with only 2 players
                            if (countingPlayers.Count < 3)
                                sumOfPositions += (3 - countingPlayers.Count) * tournament.TournamentPlayers.Count;

                            double totalWins = countingPlayers.Sum(tp =>
                                swissStats.PlayerStats.TryGetValue(tp.PlayerId, out var s) ? s.Wins : 0);

                            teamScores.Add((teamName, sumOfPositions, totalWins, countingPlayers));
                        }

                        // Rank teams
                        var rankedTeams = teamScores
                            .OrderBy(t => t.SumPositions)
                            .ThenByDescending(t => t.TotalWins)
                            .ToList();

                        for (int i = 0; i < rankedTeams.Count; i++)
                        {
                            int teamPosition = i + 1;
                            // Handle ties
                            if (i > 0 && rankedTeams[i].SumPositions == rankedTeams[i - 1].SumPositions
                                      && rankedTeams[i].TotalWins == rankedTeams[i - 1].TotalWins)
                            {
                                teamPosition = rankedTeams[i - 1].CountingPlayers.First().TeamPosition ?? i;
                            }

                            // Assign team position to counting players only (best 3)
                            foreach (var player in rankedTeams[i].CountingPlayers)
                            {
                                player.TeamPosition = teamPosition;
                            }
                        }
                    }
                    else
                    {
                        // Team mode: Swiss-system based on team results
                        // Teams with only 1 player are not qualified
                        var matchesWithRounds = tournament.Matches.Where(m => m.Round.HasValue).ToList();
                        var maxRound = matchesWithRounds.Any() ? matchesWithRounds.Max(m => m.Round) ?? 0 : 0;
                        var teamStandings = CalculateTeamStandings(
                            tournament, swissStats, positions,
                            new Dictionary<string, Dictionary<int, RoundResult>>(),
                            maxRound);

                        foreach (var team in teamStandings)
                        {
                            // Get top 3 players by position for this team
                            var teamPlayers = tournament.TournamentPlayers
                                .Where(tp => tp.Team == team.TeamName)
                                .OrderBy(tp => tp.Position ?? positions.GetValueOrDefault(tp.PlayerId, int.MaxValue))
                                .Take(3)
                                .ToList();

                            // Only assign if team has more than 1 player
                            if (teamPlayers.Count >= 2)
                            {
                                foreach (var player in teamPlayers)
                                {
                                    player.TeamPosition = team.Position;
                                }
                            }
                        }
                    }
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

