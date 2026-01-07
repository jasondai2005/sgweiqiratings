using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PlayerRatings.Engine.Rating;
using PlayerRatings.Models;
using PlayerRatings.Services;
using PlayerRatings.Util;
using PlayerRatings.ViewModels.Tournament;

namespace PlayerRatings.Controllers
{
    [Authorize]
    public class TournamentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMemoryCache _cache;
        
        // Cache duration for league matches (5 minutes)
        private static readonly TimeSpan MatchesCacheDuration = TimeSpan.FromMinutes(5);

        public TournamentsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IMemoryCache cache)
        {
            _context = context;
            _userManager = userManager;
            _cache = cache;
        }
        
        /// <summary>
        /// Gets league matches from cache, or loads from database if not cached.
        /// Only loads matches up to the specified cutoff date for efficiency.
        /// </summary>
        private async Task<List<Match>> GetLeagueMatchesAsync(Guid leagueId, DateTimeOffset? cutoffDate = null)
        {
            string cacheKey = $"LeagueMatches_{leagueId}";
            
            if (!_cache.TryGetValue(cacheKey, out List<Match> matches))
            {
                matches = await _context.Match
                    .Where(m => m.LeagueId == leagueId)
                    .Include(m => m.FirstPlayer).ThenInclude(p => p.Rankings)
                    .Include(m => m.SecondPlayer).ThenInclude(p => p.Rankings)
                    .OrderBy(m => m.Date)
                    .AsNoTracking()
                    .ToListAsync();
                
                _cache.Set(cacheKey, matches, MatchesCacheDuration);
            }
            
            // Filter by cutoff date if specified (for historical calculations)
            if (cutoffDate.HasValue)
            {
                return matches.Where(m => m.Date <= cutoffDate.Value).ToList();
            }
            
            return matches;
        }
        
        /// <summary>
        /// Invalidates the league matches cache (call when matches are added/modified).
        /// </summary>
        private void InvalidateLeagueMatchesCache(Guid leagueId)
        {
            _cache.Remove($"LeagueMatches_{leagueId}");
        }
        
        /// <summary>
        /// Gets the SWA Only preference from cookie.
        /// </summary>
        private bool GetSwaOnlyPreference()
        {
            return Request.Cookies[HomeController.SwaOnlyCookieName] == "true";
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
            int maxRound,
            Dictionary<string, (double rating, bool isRanked)> ratingsBefore = null,
            Dictionary<string, (double rating, bool isRanked)> ratingsAfter = null)
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
                        .ThenByDescending(tp => playerSwissStats.SOS.TryGetValue(tp.PlayerId, out var tsos) ? tsos : 0)
                        .ThenByDescending(tp => playerSwissStats.SOSOS.TryGetValue(tp.PlayerId, out var tsosos) ? tsosos : 0)
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
                            TeamPhoto = tp.TeamPhoto,
                            RoundResults = playerRoundResults.TryGetValue(tp.PlayerId, out var rounds) 
                                ? rounds 
                                : new Dictionary<int, RoundResult>()
                        });
                    }
                    
                    sumOfPositions += playerPenalty;
                    
                    // Skip teams with fewer than 2 counting players (not qualified)
                    if (countingPlayers.Count < 2)
                        continue;
                    
                    // Get saved team position from the first counting player (if any)
                    var savedTeamPosition = countingPlayers.FirstOrDefault()?.TeamPosition;
                    
                    teamStandings.Add(new TeamStandingViewModel
                    {
                        TeamName = teamName,
                        Players = teamPlayers,
                        TotalPlayerWins = totalWins,
                        SumOfPlayerPositions = sumOfPositions,
                        Position = savedTeamPosition // Use saved position if available
                    });
                }
                
                // Check if any team has saved positions
                bool hasSavedPositions = teamStandings.Any(t => t.Position.HasValue);
                
                // Rank by sum of positions (lower is better)
                var ranked = teamStandings
                    .OrderBy(t => t.SumOfPlayerPositions)
                    .ThenByDescending(t => t.TotalPlayerWins)
                    .ToList();
                
                for (int i = 0; i < ranked.Count; i++)
                {
                    ranked[i].Index = i + 1;
                    
                    // Only set calculated position if no saved position exists
                    if (!ranked[i].Position.HasValue)
                    {
                        ranked[i].Position = i + 1;
                        
                        // Handle ties
                        if (i > 0 && ranked[i].SumOfPlayerPositions == ranked[i - 1].SumOfPlayerPositions 
                                  && ranked[i].TotalPlayerWins == ranked[i - 1].TotalPlayerWins)
                        {
                            ranked[i].Position = ranked[i - 1].Position;
                        }
                    }
                }
                
                // If there are saved positions, re-sort by saved position for display
                if (hasSavedPositions)
                {
                    ranked = ranked.OrderBy(t => t.Position ?? int.MaxValue).ToList();
                    for (int i = 0; i < ranked.Count; i++)
                    {
                        ranked[i].Index = i + 1;
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
                // Tuple: (teamWin, teamLoss, teamDraw, opponentTeam, baseWins, mainPlayerBonus)
                var teamRoundWins = new Dictionary<string, Dictionary<int, (double wins, double losses, double draws, string opponentTeam, double baseWins, double mainPlayerBonus)>>();
                // Track main player matches (first match between two teams in each round)
                // Key: "round:playerId", Value: true if main player
                var mainPlayerMatches = new Dictionary<string, bool>();
                
                // For each round, determine team results based on player matchups
                for (int round = 1; round <= maxRound; round++)
                {
                    // Get all matches in this round, ordered by match date/order
                    var roundMatches = tournament.Matches
                        .Where(m => m.Round == round)
                        .OrderBy(m => m.Date)
                        .ThenBy(m => m.Id)
                        .ToList();
                    
                    // Track wins per team in this round (total with bonus)
                    var teamWinsInRound = new Dictionary<string, double>();
                    // Track base wins per team in this round (without bonus)
                    var teamBaseWinsInRound = new Dictionary<string, double>();
                    // Track main player bonus per team in this round
                    var teamMainPlayerBonusInRound = new Dictionary<string, double>();
                    var teamOpponents = new Dictionary<string, HashSet<string>>();
                    // Track which team pairs have already had their main player match
                    var teamPairMainPlayerAssigned = new HashSet<string>();
                    
                    foreach (var match in roundMatches)
                    {
                        var player1Team = tournament.TournamentPlayers.FirstOrDefault(tp => tp.PlayerId == match.FirstPlayerId)?.Team;
                        var player2Team = tournament.TournamentPlayers.FirstOrDefault(tp => tp.PlayerId == match.SecondPlayerId)?.Team;
                        
                        // Skip if first player has no team
                        if (string.IsNullOrEmpty(player1Team))
                            continue;
                        
                        // Check if this is a BYE match (no opponent or opponent has no team)
                        bool isByeMatch = string.IsNullOrEmpty(player2Team);
                        
                        // Check if this is the main player match (first match between these two teams in this round)
                        // BYE matches are not main player matches
                        bool isMainPlayerMatch = false;
                        if (!isByeMatch)
                        {
                            var teamPairKey = string.Compare(player1Team, player2Team) < 0 
                                ? $"{player1Team}:{player2Team}" 
                                : $"{player2Team}:{player1Team}";
                            isMainPlayerMatch = !teamPairMainPlayerAssigned.Contains(teamPairKey);
                            if (isMainPlayerMatch)
                            {
                                teamPairMainPlayerAssigned.Add(teamPairKey);
                                // Mark both players as main players for this round
                                mainPlayerMatches[$"{round}:{match.FirstPlayerId}"] = true;
                                mainPlayerMatches[$"{round}:{match.SecondPlayerId}"] = true;
                            }
                            
                            // Track opponents (only for non-BYE matches)
                            if (!teamOpponents.ContainsKey(player1Team))
                                teamOpponents[player1Team] = new HashSet<string>();
                            if (!teamOpponents.ContainsKey(player2Team))
                                teamOpponents[player2Team] = new HashSet<string>();
                            teamOpponents[player1Team].Add(player2Team);
                            teamOpponents[player2Team].Add(player1Team);
                        }
                        
                        // Initialize wins for player1's team
                        if (!teamWinsInRound.ContainsKey(player1Team))
                        {
                            teamWinsInRound[player1Team] = 0;
                            teamBaseWinsInRound[player1Team] = 0;
                            teamMainPlayerBonusInRound[player1Team] = 0;
                        }
                        // Initialize wins for player2's team (only if not BYE)
                        if (!isByeMatch && !teamWinsInRound.ContainsKey(player2Team))
                        {
                            teamWinsInRound[player2Team] = 0;
                            teamBaseWinsInRound[player2Team] = 0;
                            teamMainPlayerBonusInRound[player2Team] = 0;
                        }
                        
                        // Determine winner - main player wins count as 1.5 points (1 base + 0.5 bonus)
                        // BYE matches: just count the base win, no main player bonus
                        if (match.FirstPlayerScore > match.SecondPlayerScore)
                        {
                            teamBaseWinsInRound[player1Team] += 1;
                            teamWinsInRound[player1Team] += 1;
                            if (isMainPlayerMatch)
                            {
                                teamMainPlayerBonusInRound[player1Team] += 0.5;
                                teamWinsInRound[player1Team] += 0.5;
                            }
                        }
                        else if (match.SecondPlayerScore > match.FirstPlayerScore && !isByeMatch)
                        {
                            teamBaseWinsInRound[player2Team] += 1;
                            teamWinsInRound[player2Team] += 1;
                            if (isMainPlayerMatch)
                            {
                                teamMainPlayerBonusInRound[player2Team] += 0.5;
                                teamWinsInRound[player2Team] += 0.5;
                            }
                        }
                        else if (!isByeMatch)
                        {
                            // Draw: 0.5 each (0.75 for main player draw = 0.5 base + 0.25 bonus)
                            teamBaseWinsInRound[player1Team] += 0.5;
                            teamBaseWinsInRound[player2Team] += 0.5;
                            teamWinsInRound[player1Team] += 0.5;
                            teamWinsInRound[player2Team] += 0.5;
                            if (isMainPlayerMatch)
                            {
                                teamMainPlayerBonusInRound[player1Team] += 0.25;
                                teamMainPlayerBonusInRound[player2Team] += 0.25;
                                teamWinsInRound[player1Team] += 0.25;
                                teamWinsInRound[player2Team] += 0.25;
                            }
                        }
                    }
                    
                    // Now determine team vs team results by comparing scores
                    foreach (var team in teamWinsInRound.Keys)
                    {
                        if (!teamRoundWins.ContainsKey(team))
                            teamRoundWins[team] = new Dictionary<int, (double, double, double, string, double, double)>();
                        
                        var teamScore = teamWinsInRound[team];
                        var baseWins = teamBaseWinsInRound[team];
                        var mainPlayerBonus = teamMainPlayerBonusInRound[team];
                        var opponents = teamOpponents.GetValueOrDefault(team, new HashSet<string>());
                        var opponentTeam = opponents.FirstOrDefault() ?? "";
                        
                        // Get opponent's score for comparison
                        var opponentScore = !string.IsNullOrEmpty(opponentTeam) && teamWinsInRound.TryGetValue(opponentTeam, out var oppWins) 
                            ? oppWins 
                            : 0;
                        
                        // Determine team result by comparing scores (higher score wins)
                        double teamWin = 0, teamLoss = 0, teamDraw = 0;
                        if (teamScore > opponentScore)
                            teamWin = 1;
                        else if (teamScore < opponentScore)
                            teamLoss = 1;
                        else
                            teamDraw = 1;
                        
                        teamRoundWins[team][round] = (teamWin, teamLoss, teamDraw, opponentTeam, baseWins, mainPlayerBonus);
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
                        
                        var rounds = playerRoundResults.TryGetValue(tp.PlayerId, out var r) ? r : new Dictionary<int, RoundResult>();
                        
                        // Calculate main matches played and points earned
                        int mainMatchesPlayed = 0;
                        double pointsEarned = 0;
                        foreach (var roundResult in rounds.Values)
                        {
                            if (roundResult.IsMainPlayer)
                                mainMatchesPlayed++;
                            
                            if (roundResult.Won == true)
                                pointsEarned += roundResult.IsMainPlayer ? 1.5 : 1.0;
                            else if (roundResult.Won == null) // Draw
                                pointsEarned += roundResult.IsMainPlayer ? 0.75 : 0.5;
                        }
                        
                        // Get ratings and ranked status if available (reuse existing calculation results)
                        double? ratingBefore = null;
                        double? ratingAfter = null;
                        bool wasRankedBefore = false;
                        bool isRankedAfter = false;
                        
                        if (ratingsBefore != null && ratingsBefore.TryGetValue(tp.PlayerId, out var rBefore))
                        {
                            ratingBefore = rBefore.rating;
                            wasRankedBefore = rBefore.isRanked;
                        }
                        if (ratingsAfter != null && ratingsAfter.TryGetValue(tp.PlayerId, out var rAfter))
                        {
                            ratingAfter = rAfter.rating;
                            isRankedAfter = rAfter.isRanked;
                        }
                        
                        return new TeamPlayerViewModel
                        {
                            PlayerId = tp.PlayerId,
                            PlayerName = tp.Player?.DisplayName,
                            PersonalPosition = null, // Not used in team mode
                            CountsForTeamAward = true, // All players count in team mode
                            TeamPhoto = tp.TeamPhoto,
                            RoundResults = rounds,
                            MainMatchesPlayed = mainMatchesPlayed,
                            PointsEarned = pointsEarned,
                            RatingBefore = ratingBefore,
                            RatingAfter = ratingAfter,
                            RatingDelta = (wasRankedBefore && isRankedAfter && ratingBefore.HasValue && ratingAfter.HasValue) ? ratingAfter - ratingBefore : null,
                            WasRankedBefore = wasRankedBefore,
                            IsRankedAfter = isRankedAfter
                        };
                    })
                    .OrderByDescending(p => p.MainMatchesPlayed)
                    .ThenByDescending(p => p.PointsEarned)
                    .ThenBy(p => p.PlayerName)
                    .ToList();
                    
                    // Calculate main player bonus (0.5 for each main player win, 0.25 for each main player draw)
                    double mainPlayerBonus = 0;
                    foreach (var player in teamPlayers)
                    {
                        foreach (var roundResult in player.RoundResults.Values)
                        {
                            if (roundResult.IsMainPlayer)
                            {
                                if (roundResult.Won == true)
                                    mainPlayerBonus += 0.5; // Main player win bonus
                                else if (roundResult.Won == null)
                                    mainPlayerBonus += 0.25; // Main player draw bonus
                            }
                        }
                    }
                    
                    // Build team round results
                    var teamRoundResults = new Dictionary<int, TeamRoundResult>();
                    if (teamRoundWins.TryGetValue(teamName, out var roundWins))
                    {
                        foreach (var kvp in roundWins)
                        {
                            var round = kvp.Key;
                            var (wins, losses, draws, oppTeam, baseWins, mpBonus) = kvp.Value;
                            bool? won = wins > 0 ? true : (draws > 0 ? (bool?)null : false);
                            
                            teamRoundResults[round] = new TeamRoundResult
                            {
                                OpponentTeamName = oppTeam,
                                OpponentTeamIndex = 0, // Will be set after ranking
                                Won = won,
                                Score = $"{wins}:{losses}",
                                RoundPoints = baseWins,
                                RoundMainPlayerBonus = mpBonus
                            };
                        }
                    }
                    
                    var stats = teamStats.GetValueOrDefault(teamName, (0, 0, 0));
                    
                    // Get saved team position from any team member (they should all have the same saved TeamPosition)
                    var savedTeamPosition = teamGroup.Value.FirstOrDefault()?.TeamPosition;
                    
                    teamStandings.Add(new TeamStandingViewModel
                    {
                        TeamName = teamName,
                        Players = teamPlayers,
                        TeamWins = stats.wins,
                        TeamSOS = teamSOS.GetValueOrDefault(teamName, 0),
                        TeamSOSOS = teamSOSOS.GetValueOrDefault(teamName, 0),
                        TotalPlayerWins = totalPlayerWins,
                        TotalPlayerPoints = totalPlayerWins + mainPlayerBonus,
                        MainPlayerBonus = mainPlayerBonus,
                        TeamRoundResults = teamRoundResults,
                        Position = savedTeamPosition // Use saved position if available
                    });
                }
                
                // Check if any team has saved positions
                bool hasSavedPositions = teamStandings.Any(t => t.Position.HasValue);
                
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
                    teamIndexLookup[ranked[i].TeamName] = i + 1;
                    
                    // Only set calculated position if no saved position exists
                    if (!ranked[i].Position.HasValue)
                    {
                        ranked[i].Position = i + 1;
                        
                        // Handle ties
                        if (i > 0 && ranked[i].TeamWins == ranked[i - 1].TeamWins 
                                  && ranked[i].TeamSOS == ranked[i - 1].TeamSOS
                                  && ranked[i].TeamSOSOS == ranked[i - 1].TeamSOSOS)
                        {
                            ranked[i].Position = ranked[i - 1].Position;
                        }
                    }
                }
                
                // If there are saved positions, re-sort by saved position for display
                if (hasSavedPositions)
                {
                    ranked = ranked.OrderBy(t => t.Position ?? int.MaxValue).ToList();
                    // Rebuild the index lookup and indices
                    teamIndexLookup.Clear();
                    for (int i = 0; i < ranked.Count; i++)
                    {
                        ranked[i].Index = i + 1;
                        teamIndexLookup[ranked[i].TeamName] = i + 1;
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
                
                // Build player index lookup (sequential index across all teams, following team order)
                var playerIndexLookup = new Dictionary<string, int>();
                int playerIndex = 1;
                foreach (var team in ranked)
                {
                    foreach (var player in team.Players)
                    {
                        player.PlayerIndex = playerIndex;
                        playerIndexLookup[player.PlayerId] = playerIndex;
                        playerIndex++;
                    }
                }
                
                // Update opponent indices in player round results
                foreach (var team in ranked)
                {
                    foreach (var player in team.Players)
                    {
                        foreach (var roundResult in player.RoundResults.Values)
                        {
                            if (!string.IsNullOrEmpty(roundResult.OpponentId))
                            {
                                roundResult.OpponentIndex = playerIndexLookup.GetValueOrDefault(roundResult.OpponentId, 0);
                            }
                        }
                    }
                }
                
                return ranked;
            }
        }

        // GET: Tournaments?leagueId=xxx&year=2024
        public async Task<IActionResult> Index(Guid leagueId, int? year)
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
            
            // Set ViewData for navbar toggle visibility
            bool isSgLeague = league.Name?.Contains("Singapore Weiqi") ?? false;
            ViewData["IsSgLeague"] = isSgLeague;
            
            // Get SWA Only preference (only applies to SG league)
            bool swaOnly = isSgLeague && GetSwaOnlyPreference();

            var baseQuery = _context.Tournaments
                .Where(t => t.LeagueId == leagueId);
            
            // When SWA Only is on, only show SWA tournaments or international tournaments
            if (swaOnly)
            {
                var swaOrganizer = RatingCalculationHelper.MATCH_SWA.Trim();
                baseQuery = baseQuery.Where(t => 
                    (t.Organizer != null && t.Organizer.Contains(swaOrganizer)) ||
                    t.TournamentType == Tournament.TypeIntlOpen ||
                    t.TournamentType == Tournament.TypeIntlSelection);
            }
            
            // Get all available years for navigation (fetch dates first, then extract years client-side)
            var allStartDates = await baseQuery
                .Where(t => t.StartDate.HasValue)
                .Select(t => t.StartDate.Value)
                .ToListAsync();
            
            var availableYears = allStartDates
                .Select(d => d.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();
            
            // Default to current year if not specified, or latest year with tournaments
            var currentYear = year ?? (availableYears.Any() ? availableYears.First() : DateTime.Now.Year);
            
            // Filter by year using date range (EF Core can translate this to SQL)
            var yearStart = new DateTimeOffset(currentYear, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var yearEnd = new DateTimeOffset(currentYear + 1, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var tournamentsQuery = baseQuery
                .Where(t => t.StartDate.HasValue && t.StartDate >= yearStart && t.StartDate < yearEnd);
            
            var tournaments = await tournamentsQuery
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
            
            // Calculate prev/next years
            int? previousYear = availableYears.Where(y => y < currentYear).OrderByDescending(y => y).FirstOrDefault();
            int? nextYear = availableYears.Where(y => y > currentYear).OrderBy(y => y).FirstOrDefault();
            if (previousYear == 0) previousYear = null;
            if (nextYear == 0) nextYear = null;
            
            var viewModel = new TournamentListViewModel
            {
                LeagueId = leagueId,
                LeagueName = league.Name,
                IsAdmin = isAdmin,
                Tournaments = tournaments,
                CurrentYear = currentYear,
                PreviousYear = previousYear,
                NextYear = nextYear,
                AvailableYears = availableYears,
                TotalTournamentCount = tournaments.Count
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
            var hasMatches = tournament.Matches.Any();
            var tournamentStartDate = (tournament.StartDate ?? (hasMatches ? tournament.Matches.Min(m => m.Date) : DateTimeOffset.UtcNow));
            var tournamentEndDate = tournament.EndDate ?? (hasMatches ? tournament.Matches.Max(m => m.Date) : DateTimeOffset.UtcNow);
            
            // Load league matches from cache for rating calculation (more efficient)
            var leagueMatches = await GetLeagueMatchesAsync(tournament.LeagueId);
            
            // Get tournament player IDs for focused calculation
            var tournamentPlayerIds = tournament.TournamentPlayers.Select(tp => tp.PlayerId).ToHashSet();
            
            // Build a comprehensive player lookup from all loaded sources
            // This ensures players without matches in this tournament still get their ratings calculated correctly
            var allPlayers = new Dictionary<string, ApplicationUser>();
            foreach (var tp in tournament.TournamentPlayers)
            {
                if (tp.Player != null && !allPlayers.ContainsKey(tp.PlayerId))
                    allPlayers[tp.PlayerId] = tp.Player;
            }
            foreach (var match in leagueMatches)
            {
                if (match.FirstPlayer != null && !allPlayers.ContainsKey(match.FirstPlayerId))
                    allPlayers[match.FirstPlayerId] = match.FirstPlayer;
                if (match.SecondPlayer != null && !allPlayers.ContainsKey(match.SecondPlayerId))
                    allPlayers[match.SecondPlayerId] = match.SecondPlayer;
            }
            
            // Player lookup function - uses the combined player dictionary
            ApplicationUser PlayerLookup(string playerId) => 
                allPlayers.TryGetValue(playerId, out var player) ? player : null;
            
            // Determine league type for rating calculation
            bool isSgLeague = tournament.League?.Name?.Contains("Singapore Weiqi") ?? false;
            
            // Set ViewData for navbar toggle visibility
            ViewData["IsSgLeague"] = isSgLeague;
            
            // Get SWA Only preference (only applies to SG league)
            bool swaOnly = isSgLeague && GetSwaOnlyPreference();
            
            // Calculate ratings and ranked status before and after tournament using shared helper
            // Uses full rating calculation with match filtering and performance corrections
            // "Ranked" means the player would appear in the Ratings page at that date
            var ratingsBefore = RatingCalculationHelper.GetPlayerRatingsAndRankedStatus(
                leagueMatches, tournamentStartDate, swaOnly, isSgLeague, tournamentPlayerIds, PlayerLookup);
            
            var ratingsAfter = RatingCalculationHelper.GetPlayerRatingsAndRankedStatus(
                leagueMatches, tournamentEndDate, swaOnly, isSgLeague, tournamentPlayerIds, PlayerLookup);
            
            // Get promotion bonuses awarded during/after the tournament (not before it started)
            var promotionBonuses = RatingCalculationHelper.GetPromotionBonuses(
                leagueMatches, tournamentStartDate, tournamentEndDate, swaOnly, isSgLeague, tournamentPlayerIds);
            
            // Track first match date for each round (for creating new matches)
            var roundDates = new Dictionary<int, DateTimeOffset>();
            
            // Track main player matches (first match between two teams in each round)
            // Key: "round:team1:team2" (sorted), Value: true if already assigned
            var mainPlayerMatchesAssigned = new Dictionary<string, bool>();
            
            // First pass: identify main player matches for team competitions
            var playerTeamLookup = tournament.TournamentPlayers.ToDictionary(tp => tp.PlayerId, tp => tp.Team ?? "");
            
            foreach (var match in tournament.Matches.Where(m => m.Round.HasValue).OrderBy(m => m.Date).ThenBy(m => m.Id))
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
                        Score = realPlayerScore,
                        IsMainPlayer = false
                    };
                    continue;
                }
                
                // Check if this is the main player match (first match between these two teams in this round)
                var team1 = playerTeamLookup.GetValueOrDefault(match.FirstPlayerId, "");
                var team2 = playerTeamLookup.GetValueOrDefault(match.SecondPlayerId, "");
                bool isMainPlayer = false;
                
                if (!string.IsNullOrEmpty(team1) && !string.IsNullOrEmpty(team2) && team1 != team2)
                {
                    var teamPairKey = string.Compare(team1, team2) < 0 
                        ? $"{round}:{team1}:{team2}" 
                        : $"{round}:{team2}:{team1}";
                    
                    if (!mainPlayerMatchesAssigned.ContainsKey(teamPairKey))
                    {
                        mainPlayerMatchesAssigned[teamPairKey] = true;
                        isMainPlayer = true;
                    }
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
                    Score = $"{match.FirstPlayerScore}:{match.SecondPlayerScore}",
                    IsMainPlayer = isMainPlayer
                };
                
                // Add results for second player
                playerRoundResults[match.SecondPlayerId][round] = new RoundResult
                {
                    OpponentId = match.FirstPlayerId,
                    OpponentName = match.FirstPlayer?.DisplayName,
                    Won = firstWon.HasValue ? !firstWon.Value : null,
                    Score = $"{match.SecondPlayerScore}:{match.FirstPlayerScore}",
                    IsMainPlayer = isMainPlayer
                };
            }
            
            // Build player lookup for display names
            var playerLookup = tournament.TournamentPlayers.ToDictionary(tp => tp.PlayerId, tp => tp.Player);
            
            // Run ELO calculation on league matches to populate OldFirstPlayerRating, OldSecondPlayerRating, ShiftRating
            // This is the same calculation done in the Ratings page
            RatingCalculationHelper.CalculateRatings(leagueMatches, tournamentEndDate, swaOnly, isSgLeague);
            
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
                    .ThenByDescending(tp => swissStats.SOS.TryGetValue(tp.PlayerId, out var fsos) ? fsos : 0)
                    .ThenByDescending(tp => swissStats.SOSOS.TryGetValue(tp.PlayerId, out var fsosos) ? fsosos : 0)
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
                    maxRound,
                    ratingsBefore,
                    ratingsAfter);
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
                        ShiftRating = m.ShiftRating,
                        // Photo and game record fields
                        MatchPhoto = m.MatchPhoto,
                        MatchResultPhoto = m.MatchResultPhoto,
                        GameRecord = m.GameRecord
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
                            IsFemale = isFemale,
                            // Photo fields
                            Photo = tp.Photo,
                            TeamPhoto = tp.TeamPhoto,
                            FemaleAwardPhoto = tp.FemaleAwardPhoto
                        };
                    })
                    .OrderBy(p => p.DisplayPosition)
                    .ThenByDescending(p => p.Wins)
                    .ThenByDescending(p => p.SOS)
                    .ThenByDescending(p => p.SOSOS)
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
        public async Task<IActionResult> Edit(Guid id)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .Include(t => t.Matches)
                .Include(t => t.TournamentPlayers)
                    .ThenInclude(tp => tp.Player)
                .Include(t => t.TournamentPlayers)
                    .ThenInclude(tp => tp.Promotion)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

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
                SelectedMatchIds = selectedMatchIds.ToList(),
                SelectedPlayers = tournament.TournamentPlayers.Select(tp => new TournamentPlayerEditModel
                {
                    PlayerId = tp.PlayerId,
                    Position = tp.Position,
                    PromotionId = tp.PromotionId
                }).ToList(),
                AvailablePlayers = tournament.TournamentPlayers
                    .OrderBy(tp => tp.Position ?? int.MaxValue)
                    .ThenByDescending(tp => swissStats.PlayerStats.TryGetValue(tp.PlayerId, out var ws) ? ws.Wins : 0)
                    .ThenByDescending(tp => swissStats.SOS.TryGetValue(tp.PlayerId, out var sos1) ? sos1 : 0)
                    .ThenByDescending(tp => swissStats.SOSOS.TryGetValue(tp.PlayerId, out var sosos1) ? sosos1 : 0)
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
            
            // Update all matches in the tournament to use the new factor
            if (model.Factor.HasValue)
            {
                foreach (var match in tournament.Matches)
                {
                    match.Factor = model.Factor.Value;
                }
            }
            
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
                        // Only save personal positions if SupportsPersonalAward is true
                        if (model.PositionValues != null && i < model.PositionValues.Count)
                            tournamentPlayer.Position = tournament.SupportsPersonalAward ? model.PositionValues[i] : null;
                        
                        if (model.TeamValues != null && i < model.TeamValues.Count)
                            tournamentPlayer.Team = string.IsNullOrWhiteSpace(model.TeamValues[i]) ? null : model.TeamValues[i].Trim();
                        
                        // Only save team positions if SupportsTeamAward is true
                        if (model.TeamPositionValues != null && i < model.TeamPositionValues.Count)
                            tournamentPlayer.TeamPosition = tournament.SupportsTeamAward ? model.TeamPositionValues[i] : null;
                        
                        // Only save female positions if SupportsFemaleAward is true
                        if (model.FemalePositionValues != null && i < model.FemalePositionValues.Count)
                            tournamentPlayer.FemalePosition = tournament.SupportsFemaleAward ? model.FemalePositionValues[i] : null;
                    }
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Edit), new { id = tournament.Id });
        }

        // GET: Tournaments/SelectMatches/5
        public async Task<IActionResult> SelectMatches(Guid id, int? filterMonth, int? filterYear, string filterMatchName)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .Include(t => t.Matches)
                    .ThenInclude(m => m.FirstPlayer)
                .Include(t => t.Matches)
                    .ThenInclude(m => m.SecondPlayer)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            // Default filter to tournament dates or current month
            if (!filterYear.HasValue)
            {
                filterYear = tournament.StartDate?.Year ?? DateTimeOffset.UtcNow.Year;
            }
            if (!filterMonth.HasValue)
            {
                filterMonth = tournament.StartDate?.Month ?? DateTimeOffset.UtcNow.Month;
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

            var viewModel = new TournamentEditViewModel
            {
                Id = tournament.Id,
                LeagueId = tournament.LeagueId,
                LeagueName = tournament.League.Name,
                Name = tournament.Name,
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
                }).ToList()
            };

            return View(viewModel);
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
                return RedirectToAction(nameof(SelectMatches), new { id = tournamentId, filterMonth, filterYear, filterMatchName });
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
            
            // Invalidate cache since matches were modified
            InvalidateLeagueMatchesCache(tournament.LeagueId);

            return RedirectToAction(nameof(SelectMatches), new { id = tournamentId, filterMonth, filterYear, filterMatchName });
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
                return RedirectToAction(nameof(SelectMatches), new { id = tournamentId, filterMonth, filterYear, filterMatchName });
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
            
            // Invalidate cache since match rounds were modified
            InvalidateLeagueMatchesCache(tournament.LeagueId);

            return RedirectToAction(nameof(SelectMatches), new { id = tournamentId, filterMonth, filterYear, filterMatchName });
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
                return RedirectToAction(nameof(SelectMatches), new { id = tournamentId, filterMonth, filterYear, filterMatchName });
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
            
            // Invalidate cache since match dates were modified
            InvalidateLeagueMatchesCache(tournament.LeagueId);

            return RedirectToAction(nameof(SelectMatches), new { id = tournamentId, filterMonth, filterYear, filterMatchName });
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
                
                // Invalidate cache since match was removed from tournament
                InvalidateLeagueMatchesCache(tournament.LeagueId);
            }

            return RedirectToAction(nameof(SelectMatches), new { id = tournamentId, filterMonth, filterYear, filterMatchName });
        }

        // POST: Tournaments/RemovePlayer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePlayer(Guid tournamentId, string playerId)
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

            return RedirectToAction(nameof(Edit), new { id = tournamentId });
        }

        // POST: Tournaments/AddPlayer - manually add a player to tournament
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPlayer(Guid tournamentId, string playerId)
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

            return RedirectToAction(nameof(Edit), new { id = tournamentId });
        }

        // POST: Tournaments/CalculatePositions
        // Uses Swiss-system: undefeated players are champions, then rank by wins → SOS → SOSOS
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CalculatePositions(Guid tournamentId)
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

            // Update personal positions in database (only if SupportsPersonalAward is true)
            foreach (var kvp in positions)
            {
                var tournamentPlayer = tournament.TournamentPlayers.FirstOrDefault(tp => tp.PlayerId == kvp.Key);
                if (tournamentPlayer != null)
                {
                    // If personal award is not supported, set position to null
                    tournamentPlayer.Position = tournament.SupportsPersonalAward ? kvp.Value : (int?)null;
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
                    .ThenByDescending(tp => swissStats.SOS.TryGetValue(tp.PlayerId, out var fsos) ? fsos : 0)
                    .ThenByDescending(tp => swissStats.SOSOS.TryGetValue(tp.PlayerId, out var fsosos) ? fsosos : 0)
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
                                .ThenByDescending(tp => swissStats.SOS.TryGetValue(tp.PlayerId, out var tsos) ? tsos : 0)
                                .ThenByDescending(tp => swissStats.SOSOS.TryGetValue(tp.PlayerId, out var tsosos) ? tsosos : 0)
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
                            // Get all players for this team (team competition counts all players)
                            var teamPlayers = tournament.TournamentPlayers
                                .Where(tp => tp.Team == team.TeamName)
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

            return RedirectToAction(nameof(Edit), new { id = tournamentId });
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
            
            // Invalidate cache since tournament was deleted
            InvalidateLeagueMatchesCache(leagueId);

            return RedirectToAction(nameof(Index), new { leagueId });
        }

        // GET: Tournaments/EditPhoto - Page for editing photos/game records
        public async Task<IActionResult> EditPhoto(Guid tournamentId, string playerId, Guid? matchId, string photoType)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .FirstOrDefaultAsync(t => t.Id == tournamentId);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            string currentUrl = null;
            string title = "Edit Photo";

            switch (photoType?.ToLower())
            {
                case "standings_photo":
                case "standingsphoto": // backwards compatibility
                    currentUrl = tournament.StandingsPhoto;
                    title = "Standings Photo";
                    break;
                case "photo":
                case "teamphoto":
                case "femaleawardphoto":
                    if (!string.IsNullOrEmpty(playerId))
                    {
                        var tp = await _context.TournamentPlayers.FirstOrDefaultAsync(t => t.TournamentId == tournamentId && t.PlayerId == playerId);
                        if (tp != null)
                        {
                            currentUrl = photoType == "photo" ? tp.Photo : (photoType == "teamphoto" ? tp.TeamPhoto : tp.FemaleAwardPhoto);
                            title = photoType == "teamphoto" ? "Team Photo" : "Player Photo";
                        }
                    }
                    break;
                case "matchphoto":
                case "matchresultphoto":
                case "gamerecord":
                    if (matchId.HasValue)
                    {
                        var match = await _context.Match.FirstOrDefaultAsync(m => m.Id == matchId.Value);
                        if (match != null)
                        {
                            currentUrl = photoType == "matchphoto" ? match.MatchPhoto : (photoType == "matchresultphoto" ? match.MatchResultPhoto : match.GameRecord);
                            title = photoType == "gamerecord" ? "Game Record" : (photoType == "matchphoto" ? "Match Photo" : "Result Photo");
                        }
                    }
                    break;
            }

            ViewBag.TournamentId = tournamentId;
            ViewBag.PlayerId = playerId;
            ViewBag.MatchId = matchId;
            ViewBag.PhotoType = photoType;
            ViewBag.CurrentUrl = currentUrl;
            ViewBag.Title = title;
            ViewBag.IsGameRecord = photoType == "gamerecord";
            ViewBag.IsStandingsPhoto = photoType == "standings_photo";

            return View();
        }

        // POST: Tournaments/EditPhoto - Save photo URL or upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPhoto(Guid tournamentId, string playerId, Guid? matchId, string photoType, string photoUrl, IFormFile photoFile)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .FirstOrDefaultAsync(t => t.Id == tournamentId);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            string finalUrl = photoUrl?.Trim();

            // Handle file upload if provided
            if (photoFile != null && photoFile.Length > 0)
            {
                bool isGameRecord = photoType?.ToLower() == "gamerecord";
                bool isStandingsPhoto = photoType?.ToLower() == "standings_photo";
                
                if (isGameRecord)
                {
                    // Allow SGF and TXT files for game records
                    var extension = Path.GetExtension(photoFile.FileName).ToLower();
                    if (extension != ".sgf" && extension != ".txt")
                    {
                        TempData["Error"] = "Invalid file type. Allowed: SGF, TXT";
                        return RedirectToAction(nameof(EditPhoto), new { tournamentId, playerId, matchId, photoType });
                    }

                    if (photoFile.Length > 1 * 1024 * 1024) // 1MB limit for SGF files
                    {
                        TempData["Error"] = "File too large. Maximum size: 1MB";
                        return RedirectToAction(nameof(EditPhoto), new { tournamentId, playerId, matchId, photoType });
                    }
                }
                else if (isStandingsPhoto)
                {
                    // Allow images and PDF for standings photo
                    var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp", "application/pdf" };
                    if (!allowedTypes.Contains(photoFile.ContentType.ToLower()))
                    {
                        TempData["Error"] = "Invalid file type. Allowed: JPG, PNG, GIF, WebP, PDF";
                        return RedirectToAction(nameof(EditPhoto), new { tournamentId, playerId, matchId, photoType });
                    }

                    if (photoFile.Length > 10 * 1024 * 1024) // 10MB limit for PDFs
                    {
                        TempData["Error"] = "File too large. Maximum size: 10MB";
                        return RedirectToAction(nameof(EditPhoto), new { tournamentId, playerId, matchId, photoType });
                    }
                }
                else
                {
                    var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
                    if (!allowedTypes.Contains(photoFile.ContentType.ToLower()))
                    {
                        TempData["Error"] = "Invalid file type. Allowed: JPG, PNG, GIF, WebP";
                        return RedirectToAction(nameof(EditPhoto), new { tournamentId, playerId, matchId, photoType });
                    }

                    if (photoFile.Length > 5 * 1024 * 1024)
                    {
                        TempData["Error"] = "File too large. Maximum size: 5MB";
                        return RedirectToAction(nameof(EditPhoto), new { tournamentId, playerId, matchId, photoType });
                    }
                }

                var fileExtension = Path.GetExtension(photoFile.FileName).ToLower();
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "tournament");
                
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var filePath = Path.Combine(uploadsFolder, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await photoFile.CopyToAsync(stream);
                }

                finalUrl = $"/uploads/tournament/{fileName}";
            }

            // Save to database
            switch (photoType?.ToLower())
            {
                case "standings_photo":
                case "standingsphoto": // backwards compatibility
                    tournament.StandingsPhoto = string.IsNullOrWhiteSpace(finalUrl) ? null : finalUrl;
                    break;
                case "photo":
                case "teamphoto":
                case "femaleawardphoto":
                    if (!string.IsNullOrEmpty(playerId))
                    {
                        var tp = await _context.TournamentPlayers.FirstOrDefaultAsync(t => t.TournamentId == tournamentId && t.PlayerId == playerId);
                        if (tp != null)
                        {
                            if (photoType == "photo") tp.Photo = string.IsNullOrWhiteSpace(finalUrl) ? null : finalUrl;
                            else if (photoType == "teamphoto") tp.TeamPhoto = string.IsNullOrWhiteSpace(finalUrl) ? null : finalUrl;
                            else if (photoType == "femaleawardphoto") tp.FemaleAwardPhoto = string.IsNullOrWhiteSpace(finalUrl) ? null : finalUrl;
                        }
                    }
                    break;
                case "matchphoto":
                case "matchresultphoto":
                case "gamerecord":
                    if (matchId.HasValue)
                    {
                        var match = await _context.Match.FirstOrDefaultAsync(m => m.Id == matchId.Value);
                        if (match != null)
                        {
                            if (photoType == "matchphoto") match.MatchPhoto = string.IsNullOrWhiteSpace(finalUrl) ? null : finalUrl;
                            else if (photoType == "matchresultphoto") match.MatchResultPhoto = string.IsNullOrWhiteSpace(finalUrl) ? null : finalUrl;
                            else if (photoType == "gamerecord") match.GameRecord = string.IsNullOrWhiteSpace(finalUrl) ? null : finalUrl;
                        }
                    }
                    break;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Saved successfully!";
            return View("EditPhotoSuccess");
        }

        // POST: Tournaments/UploadTournamentPhoto - AJAX endpoint to upload a photo file (kept for compatibility)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadTournamentPhoto(IFormFile photoFile, string photoType, Guid? tournamentId, Guid? matchId, string playerId)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            if (photoFile == null || photoFile.Length == 0)
            {
                return Json(new { success = false, message = "No file uploaded" });
            }

            // Validate file type
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
            if (!allowedTypes.Contains(photoFile.ContentType.ToLower()))
            {
                return Json(new { success = false, message = "Invalid file type. Allowed: JPG, PNG, GIF, WebP" });
            }

            // Validate file size (max 5MB)
            if (photoFile.Length > 5 * 1024 * 1024)
            {
                return Json(new { success = false, message = "File too large. Maximum size: 5MB" });
            }

            try
            {
                // Generate unique filename
                var extension = Path.GetExtension(photoFile.FileName).ToLower();
                var fileName = $"{Guid.NewGuid()}{extension}";
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "tournament");
                
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var filePath = Path.Combine(uploadsFolder, fileName);
                
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await photoFile.CopyToAsync(stream);
                }

                var photoUrl = $"/uploads/tournament/{fileName}";

                // Update the appropriate record based on photoType
                switch (photoType?.ToLower())
                {
                    case "standings_photo":
                    case "standingsphoto": // backwards compatibility
                        if (tournamentId.HasValue)
                        {
                            var tournament = await _context.Tournaments.Include(t => t.League).FirstOrDefaultAsync(t => t.Id == tournamentId.Value);
                            if (tournament != null && tournament.League.CreatedByUserId == currentUser.Id)
                            {
                                tournament.StandingsPhoto = photoUrl;
                                await _context.SaveChangesAsync();
                            }
                        }
                        break;
                    case "photo":
                    case "teamphoto":
                    case "femaleawardphoto":
                        if (tournamentId.HasValue && !string.IsNullOrEmpty(playerId))
                        {
                            var tp = await _context.TournamentPlayers
                                .Include(t => t.Tournament).ThenInclude(t => t.League)
                                .FirstOrDefaultAsync(t => t.TournamentId == tournamentId.Value && t.PlayerId == playerId);
                            if (tp != null && tp.Tournament.League.CreatedByUserId == currentUser.Id)
                            {
                                if (photoType == "photo") tp.Photo = photoUrl;
                                else if (photoType == "teamphoto") tp.TeamPhoto = photoUrl;
                                else if (photoType == "femaleawardphoto") tp.FemaleAwardPhoto = photoUrl;
                                await _context.SaveChangesAsync();
                            }
                        }
                        break;
                    case "matchphoto":
                    case "matchresultphoto":
                        if (matchId.HasValue)
                        {
                            var match = await _context.Match.Include(m => m.League).FirstOrDefaultAsync(m => m.Id == matchId.Value);
                            if (match != null && match.League.CreatedByUserId == currentUser.Id)
                            {
                                if (photoType == "matchphoto") match.MatchPhoto = photoUrl;
                                else if (photoType == "matchresultphoto") match.MatchResultPhoto = photoUrl;
                                await _context.SaveChangesAsync();
                            }
                        }
                        break;
                    default:
                        return Json(new { success = false, message = "Invalid photo type" });
                }

                return Json(new { success = true, photoUrl = photoUrl });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error uploading file: " + ex.Message });
            }
        }

        // POST: Tournaments/UpdateStandingsPhoto - AJAX endpoint to update tournament standings photo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStandingsPhoto(Guid tournamentId, string photoUrl)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .FirstOrDefaultAsync(t => t.Id == tournamentId);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return Json(new { success = false, message = "Not authorized" });
            }

            tournament.StandingsPhoto = string.IsNullOrWhiteSpace(photoUrl) ? null : photoUrl.Trim();
            await _context.SaveChangesAsync();
            
            return Json(new { success = true, photoUrl = tournament.StandingsPhoto });
        }

        // POST: Tournaments/UpdatePlayerPhoto - AJAX endpoint to update tournament player photo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePlayerPhoto(Guid tournamentId, string playerId, string photoType, string photoUrl)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .Include(t => t.TournamentPlayers)
                .FirstOrDefaultAsync(t => t.Id == tournamentId);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return Json(new { success = false, message = "Not authorized" });
            }

            var tournamentPlayer = tournament.TournamentPlayers.FirstOrDefault(tp => tp.PlayerId == playerId);
            if (tournamentPlayer == null)
            {
                return Json(new { success = false, message = "Player not found" });
            }

            // Sanitize URL (basic validation)
            var url = string.IsNullOrWhiteSpace(photoUrl) ? null : photoUrl.Trim();

            switch (photoType?.ToLower())
            {
                case "photo":
                    tournamentPlayer.Photo = url;
                    break;
                case "teamphoto":
                    tournamentPlayer.TeamPhoto = url;
                    break;
                case "femaleawardphoto":
                    tournamentPlayer.FemaleAwardPhoto = url;
                    break;
                default:
                    return Json(new { success = false, message = "Invalid photo type" });
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, photoUrl = url });
        }

        // POST: Tournaments/UpdateMatchMedia - AJAX endpoint to update match photo/game record
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMatchMedia(Guid matchId, string mediaType, string mediaUrl)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var match = await _context.Match
                .Include(m => m.League)
                .FirstOrDefaultAsync(m => m.Id == matchId);

            if (match == null || match.League.CreatedByUserId != currentUser.Id)
            {
                return Json(new { success = false, message = "Not authorized" });
            }

            // Sanitize URL (basic validation)
            var url = string.IsNullOrWhiteSpace(mediaUrl) ? null : mediaUrl.Trim();

            switch (mediaType?.ToLower())
            {
                case "matchphoto":
                    match.MatchPhoto = url;
                    break;
                case "matchresultphoto":
                    match.MatchResultPhoto = url;
                    break;
                case "gamerecord":
                    match.GameRecord = url;
                    break;
                default:
                    return Json(new { success = false, message = "Invalid media type" });
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, mediaUrl = url });
        }

        // GET: Tournaments/ImportH9 - Show H9 file upload form
        public async Task<IActionResult> ImportH9(Guid id)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            return View(new ImportH9UploadViewModel
            {
                TournamentId = tournament.Id,
                TournamentName = tournament.FullName,
                LeagueId = tournament.LeagueId
            });
        }

        // POST: Tournaments/ImportH9 - Process uploaded H9 file
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportH9(ImportH9UploadViewModel model)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .FirstOrDefaultAsync(t => t.Id == model.TournamentId);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            if (model.H9File == null || model.H9File.Length == 0)
            {
                ModelState.AddModelError("H9File", "Please select an H9 file");
                model.TournamentName = tournament.FullName;
                return View(model);
            }

            // Validate file type - accept H9 (XML), or text-based tournament files
            var extension = Path.GetExtension(model.H9File.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".h9", ".xml", ".txt", ".tou", ".egf" };
            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("H9File", "Please upload a valid tournament file (.h9, .xml, .txt, .tou, .egf)");
                model.TournamentName = tournament.FullName;
                return View(model);
            }

            // Parse H9 file
            var parser = new H9FileParser();
            H9ParseResult parseResult;
            using (var stream = model.H9File.OpenReadStream())
            {
                parseResult = parser.Parse(stream);
            }

            if (!parseResult.Success)
            {
                ModelState.AddModelError("H9File", parseResult.ErrorMessage);
                model.TournamentName = tournament.FullName;
                return View(model);
            }

            // Show all rounds preview directly
            return await ShowImportPreviewAllRounds(tournament, parseResult);
        }

        private async Task<IActionResult> ShowImportPreviewAllRounds(Tournament tournament, H9ParseResult parseResult)
        {
            // Get all games
            var allGames = parseResult.Games.OrderBy(g => g.RoundNumber).ThenBy(g => g.TableNumber).ToList();

            // Get available players for matching
            var playerIds = await _context.LeaguePlayers
                .Where(lp => lp.LeagueId == tournament.LeagueId)
                .Select(lp => lp.UserId)
                .ToListAsync();
            
            var existingPlayers = await _context.Users
                .Where(u => playerIds.Contains(u.Id))
                .Include(u => u.Rankings)
                .OrderBy(u => u.DisplayName)
                .AsNoTracking()
                .ToListAsync();

            var playerOptions = existingPlayers.Select(p => new PlayerOption
            {
                Id = p.Id,
                DisplayName = p.DisplayName,
                Ranking = p.LatestRanking,
                SearchName = p.DisplayName?.ToLowerInvariant() ?? ""
            }).ToList();

            // Build preview model
            var previewModel = new ImportPreviewViewModel
            {
                TournamentId = tournament.Id,
                TournamentName = tournament.FullName,
                LeagueId = tournament.LeagueId,
                HasErrors = false,
                AvailablePlayers = playerOptions,
                TotalRounds = parseResult.NumberOfRounds > 0 ? parseResult.NumberOfRounds : (allGames.Any() ? allGames.Max(g => g.RoundNumber) : 0),
                H9TournamentName = parseResult.TournamentName,
                H9StartDate = parseResult.StartDate,
                H9Location = parseResult.Location
            };

            var parser = new H9FileParser();

            // Count games per player
            var playerGameCounts = new Dictionary<int, int>();
            foreach (var game in allGames)
            {
                if (!playerGameCounts.ContainsKey(game.WhitePlayerIndex))
                    playerGameCounts[game.WhitePlayerIndex] = 0;
                if (!playerGameCounts.ContainsKey(game.BlackPlayerIndex))
                    playerGameCounts[game.BlackPlayerIndex] = 0;
                playerGameCounts[game.WhitePlayerIndex]++;
                playerGameCounts[game.BlackPlayerIndex]++;
            }

            // Build player mappings - one per H9 player
            foreach (var h9Player in parseResult.Players)
            {
                var (bestMatch, confidence) = FindBestPlayerMatch(h9Player.FullName, existingPlayers);
                
                var mapping = new PlayerMappingViewModel
                {
                    PlayerIndex = h9Player.PlayerIndex,
                    ExtractedName = h9Player.FullName,
                    ExtractedRanking = h9Player.Rank,
                    GamesCount = playerGameCounts.GetValueOrDefault(h9Player.PlayerIndex, 0),
                    NewPlayerName = h9Player.FullName,
                    NewPlayerRanking = h9Player.Rank
                };

                if (bestMatch != null)
                {
                    mapping.SuggestedPlayerId = bestMatch.Id;
                    mapping.SuggestedPlayerName = bestMatch.DisplayName;
                    mapping.MatchConfidence = confidence;
                    if (confidence >= 0.8)
                    {
                        mapping.SelectedPlayerId = bestMatch.Id;
                    }
                }

                previewModel.PlayerMappings.Add(mapping);
            }

            // Build match list (including BYE games)
            for (int i = 0; i < allGames.Count; i++)
            {
                var game = allGames[i];
                var whitePlayer = parser.GetPlayer(parseResult, game.WhitePlayerIndex);
                
                // For BYE games, black player index is -1
                var isBye = game.IsBye || game.BlackPlayerIndex == H9FileParser.BYE_INDEX;
                var blackPlayer = isBye ? null : parser.GetPlayer(parseResult, game.BlackPlayerIndex);

                // Skip if white player is missing (shouldn't happen)
                if (whitePlayer == null)
                    continue;

                var matchVm = new ExtractedMatchViewModel
                {
                    Index = i,
                    RoundNumber = game.RoundNumber,
                    TableNumber = game.TableNumber,
                    WhitePlayerIndex = game.WhitePlayerIndex,
                    BlackPlayerIndex = game.BlackPlayerIndex,
                    WhitePlayerName = whitePlayer.FullName,
                    BlackPlayerName = isBye ? "BYE" : blackPlayer?.FullName ?? "Unknown",
                    WhiteScore = game.WhiteScore,
                    BlackScore = game.BlackScore,
                    Handicap = game.Handicap,
                    Include = true,
                    IsBye = isBye
                };

                previewModel.Matches.Add(matchVm);
            }

            return View("ImportPreview", previewModel);
        }

        private string SerializeH9Data(H9ParseResult parseResult)
        {
            var data = new
            {
                parseResult.TournamentName,
                parseResult.StartDate,
                parseResult.EndDate,
                parseResult.Location,
                parseResult.NumberOfRounds,
                Players = parseResult.Players.Select(p => new { p.PlayerIndex, p.Name, p.FirstName, p.Rank, p.Country, p.Club }),
                Games = parseResult.Games.Select(g => new { g.RoundNumber, g.TableNumber, g.WhitePlayerIndex, g.BlackPlayerIndex, g.Result, g.Handicap })
            };
            return System.Text.Json.JsonSerializer.Serialize(data);
        }

        private H9ParseResult DeserializeH9Data(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                var result = new H9ParseResult
                {
                    Success = true,
                    TournamentName = root.GetProperty("TournamentName").GetString(),
                    Location = root.TryGetProperty("Location", out var loc) ? loc.GetString() : null,
                    NumberOfRounds = root.TryGetProperty("NumberOfRounds", out var nr) ? nr.GetInt32() : 0
                };

                if (root.TryGetProperty("StartDate", out var sd) && sd.ValueKind != System.Text.Json.JsonValueKind.Null)
                    result.StartDate = sd.GetDateTimeOffset();

                if (root.TryGetProperty("EndDate", out var ed) && ed.ValueKind != System.Text.Json.JsonValueKind.Null)
                    result.EndDate = ed.GetDateTimeOffset();

                foreach (var p in root.GetProperty("Players").EnumerateArray())
                {
                    result.Players.Add(new H9Player
                    {
                        PlayerIndex = p.GetProperty("PlayerIndex").GetInt32(),
                        Name = p.GetProperty("Name").GetString(),
                        FirstName = p.GetProperty("FirstName").GetString(),
                        Rank = p.TryGetProperty("Rank", out var rank) ? rank.GetString() : "",
                        Country = p.TryGetProperty("Country", out var country) ? country.GetString() : "",
                        Club = p.TryGetProperty("Club", out var club) ? club.GetString() : ""
                    });
                }

                foreach (var g in root.GetProperty("Games").EnumerateArray())
                {
                    result.Games.Add(new H9Game
                    {
                        RoundNumber = g.GetProperty("RoundNumber").GetInt32(),
                        TableNumber = g.GetProperty("TableNumber").GetInt32(),
                        WhitePlayerIndex = g.GetProperty("WhitePlayerIndex").GetInt32(),
                        BlackPlayerIndex = g.GetProperty("BlackPlayerIndex").GetInt32(),
                        Result = g.GetProperty("Result").GetString(),
                        Handicap = g.TryGetProperty("Handicap", out var hc) ? hc.GetInt32() : 0
                    });
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        // POST: Tournaments/ImportConfirm - Create matches from confirmed import
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportConfirm(ImportConfirmViewModel model)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            var tournament = await _context.Tournaments
                .Include(t => t.League)
                .Include(t => t.TournamentPlayers)
                .FirstOrDefaultAsync(t => t.Id == model.TournamentId);

            if (tournament == null || tournament.League.CreatedByUserId != currentUser.Id)
            {
                return NotFound();
            }

            // First, resolve all player mappings to ApplicationUser instances
            var resolvedPlayers = new Dictionary<int, ApplicationUser>(); // key: H9 player index
            
            foreach (var mapping in model.PlayerMappings)
            {
                ApplicationUser player = null;
                
                if (mapping.CreateNewPlayer && !string.IsNullOrEmpty(mapping.NewPlayerName))
                {
                    // Check if player with same name already exists
                    player = await _context.Users
                        .FirstOrDefaultAsync(u => u.DisplayName == mapping.NewPlayerName);

                    if (player == null)
                    {
                        // Create new player
                        player = new ApplicationUser
                        {
                            UserName = Guid.NewGuid().ToString(),
                            Email = $"{Guid.NewGuid()}@placeholder.local",
                            DisplayName = mapping.NewPlayerName
                        };
                        await _userManager.CreateAsync(player);

                        // Add ranking if provided
                        if (!string.IsNullOrEmpty(mapping.NewPlayerRanking))
                        {
                            // Determine organization from ranking format:
                            // - "(2D)" format indicates TGA ranking
                            // - "[2D]" format indicates foreign ranking  
                            // - Plain "2D" format indicates SWA ranking (default for SG)
                            string organization = "SWA"; // Default to SWA for Singapore players
                            string rankingValue = mapping.NewPlayerRanking.Trim();
                            
                            if (rankingValue.StartsWith("(") && rankingValue.EndsWith(")"))
                            {
                                organization = "TGA";
                                rankingValue = rankingValue.Trim('(', ')');
                            }
                            else if (rankingValue.StartsWith("[") && rankingValue.EndsWith("]"))
                            {
                                organization = "Foreign";
                                rankingValue = rankingValue.Trim('[', ']');
                            }
                            
                            var ranking = new PlayerRanking
                            {
                                RankingId = Guid.NewGuid(),
                                PlayerId = player.Id,
                                Ranking = rankingValue.ToUpperInvariant(),
                                RankingDate = model.MatchDate,
                                Organization = organization
                            };
                            _context.PlayerRankings.Add(ranking);
                        }
                    }

                    // Add to league if not already there
                    if (!_context.LeaguePlayers.Any(lp => lp.LeagueId == tournament.LeagueId && lp.UserId == player.Id))
                    {
                        _context.LeaguePlayers.Add(new LeaguePlayer
                        {
                            Id = Guid.NewGuid(),
                            LeagueId = tournament.LeagueId,
                            UserId = player.Id
                        });
                    }
                }
                else if (!string.IsNullOrEmpty(mapping.SelectedPlayerId))
                {
                    player = await _context.Users.FindAsync(mapping.SelectedPlayerId);
                }

                if (player != null)
                {
                    resolvedPlayers[mapping.PlayerIndex] = player;
                }
            }

            var createdMatches = new List<Match>();

            foreach (var matchToImport in model.Matches.Where(m => m.Include))
            {
                // Look up white player from the resolved mappings
                if (!resolvedPlayers.TryGetValue(matchToImport.WhitePlayerIndex, out var whitePlayer))
                {
                    // Skip if white player mapping is missing
                    continue;
                }

                // For BYE matches, black player is null
                ApplicationUser blackPlayer = null;
                if (!matchToImport.IsBye)
                {
                    if (!resolvedPlayers.TryGetValue(matchToImport.BlackPlayerIndex, out blackPlayer))
                    {
                        // Skip if black player mapping is missing (for non-BYE matches)
                        continue;
                    }
                }

                // Calculate time offset: each match gets a unique timestamp
                // Round number * 100 seconds + match count within import
                // This ensures matches are processed in correct order by the rating system
                var roundNum = matchToImport.RoundNumber > 0 ? matchToImport.RoundNumber : 1;
                var timeOffsetSeconds = (roundNum - 1) * 100 + createdMatches.Count;
                var matchDate = model.MatchDate.AddSeconds(timeOffsetSeconds);

                // Create match - BYE matches have Factor = 0 (don't affect ratings)
                var match = new Match
                {
                    Id = Guid.NewGuid(),
                    LeagueId = tournament.LeagueId,
                    TournamentId = tournament.Id,
                    Round = roundNum,
                    Date = matchDate,
                    FirstPlayerId = whitePlayer.Id,
                    SecondPlayerId = blackPlayer?.Id,  // null for BYE
                    FirstPlayerScore = matchToImport.WhiteScore,
                    SecondPlayerScore = matchToImport.BlackScore,
                    Factor = matchToImport.IsBye ? 0 : model.Factor,  // BYE = factor 0
                    CreatedByUser = currentUser
                };

                _context.Match.Add(match);
                createdMatches.Add(match);

                // Add players to tournament if not already there
                await EnsurePlayerInTournament(tournament, whitePlayer.Id);
                if (blackPlayer != null)
                {
                    await EnsurePlayerInTournament(tournament, blackPlayer.Id);
                }
            }

            await _context.SaveChangesAsync();

            // Invalidate cache
            InvalidateLeagueMatchesCache(tournament.LeagueId);

            TempData["Message"] = $"Successfully imported {createdMatches.Count} matches.";
            return RedirectToAction(nameof(Details), new { id = tournament.Id });
        }

        private async Task EnsurePlayerInTournament(Tournament tournament, string playerId)
        {
            if (!tournament.TournamentPlayers.Any(tp => tp.PlayerId == playerId))
            {
                var tournamentPlayer = new TournamentPlayer
                {
                    TournamentId = tournament.Id,
                    PlayerId = playerId
                };
                _context.TournamentPlayers.Add(tournamentPlayer);
                tournament.TournamentPlayers.Add(tournamentPlayer);
            }
        }

        /// <summary>
        /// Find the best matching player based on name similarity
        /// </summary>
        private (ApplicationUser player, double confidence) FindBestPlayerMatch(string extractedName, List<ApplicationUser> players)
        {
            if (string.IsNullOrEmpty(extractedName) || !players.Any())
                return (null, 0);

            var extractedLower = NormalizeName(extractedName);
            ApplicationUser bestMatch = null;
            double bestScore = 0;

            foreach (var player in players)
            {
                var playerName = NormalizeName(player.DisplayName ?? "");
                var score = CalculateNameSimilarity(extractedLower, playerName);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = player;
                }
            }

            return (bestMatch, bestScore);
        }

        private string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            
            // Convert to lowercase, remove extra spaces
            var normalized = name.ToLowerInvariant().Trim();
            
            // Remove common variations (commas, periods)
            normalized = normalized.Replace(",", " ").Replace(".", " ");
            
            // Normalize whitespace
            while (normalized.Contains("  "))
                normalized = normalized.Replace("  ", " ");

            return normalized;
        }

        private double CalculateNameSimilarity(string name1, string name2)
        {
            if (string.IsNullOrEmpty(name1) || string.IsNullOrEmpty(name2))
                return 0;

            // Exact match
            if (name1 == name2)
                return 1.0;

            // Check if one contains the other
            if (name1.Contains(name2) || name2.Contains(name1))
                return 0.9;

            // Split into parts and check overlap
            var parts1 = name1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var parts2 = name2.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts1.Length == 0 || parts2.Length == 0)
                return 0;

            // Count matching parts
            int matches = 0;
            foreach (var p1 in parts1)
            {
                if (parts2.Any(p2 => p1 == p2 || LevenshteinDistance(p1, p2) <= 1))
                    matches++;
            }

            double overlap = (double)matches / Math.Max(parts1.Length, parts2.Length);

            // Also calculate Levenshtein similarity
            int maxLen = Math.Max(name1.Length, name2.Length);
            int distance = LevenshteinDistance(name1, name2);
            double levenshteinSim = 1.0 - (double)distance / maxLen;

            // Combine scores
            return Math.Max(overlap * 0.9, levenshteinSim);
        }

        private int LevenshteinDistance(string s1, string s2)
        {
            int[,] d = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++)
                d[i, 0] = i;
            for (int j = 0; j <= s2.Length; j++)
                d[0, j] = j;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[s1.Length, s2.Length];
        }
    }
}

