using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PlayerRatings.Services
{
    /// <summary>
    /// Represents a player from a tournament file
    /// </summary>
    public class H9Player
    {
        public string Name { get; set; }
        public string FirstName { get; set; }
        public string FullName => $"{Name}, {FirstName}".Trim(',', ' ');
        public string Rank { get; set; }
        public string Country { get; set; }
        public string Club { get; set; }
        public int PlayerIndex { get; set; } // 0-based index in the file
    }

    /// <summary>
    /// Represents a game/match from a tournament file
    /// </summary>
    public class H9Game
    {
        public int RoundNumber { get; set; }
        public int TableNumber { get; set; }
        public int WhitePlayerIndex { get; set; }
        public int BlackPlayerIndex { get; set; }
        public string Result { get; set; } // "RESULT_WHITEWINS", "RESULT_BLACKWINS", "RESULT_DRAW", etc.
        public int Handicap { get; set; }
        public bool IsBye { get; set; } // True if this is a BYE (no opponent)
        
        public int WhiteScore => Result switch
        {
            "RESULT_WHITEWINS" or "RESULT_WHITEWINS_BYDEF" or "RESULT_BOTHLOSE_BYDEF" => 1,
            "RESULT_DRAW" => 0, // Could be 0.5 for draws
            _ => 0
        };
        
        public int BlackScore => Result switch
        {
            "RESULT_BLACKWINS" or "RESULT_BLACKWINS_BYDEF" => 1,
            "RESULT_DRAW" => 0,
            _ => 0
        };
    }

    /// <summary>
    /// Result of parsing a tournament file
    /// </summary>
    public class H9ParseResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string TournamentName { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public string Location { get; set; }
        public int NumberOfRounds { get; set; }
        public List<H9Player> Players { get; set; } = new List<H9Player>();
        public List<H9Game> Games { get; set; } = new List<H9Game>();
    }

    /// <summary>
    /// Parser for EGF text format tournament files
    /// </summary>
    public class H9FileParser
    {
        /// <summary>
        /// Special index indicating a BYE (no opponent)
        /// </summary>
        public const int BYE_INDEX = -1;
        
        /// <summary>
        /// Parse an EGF text format tournament file from a stream.
        /// </summary>
        public H9ParseResult Parse(Stream stream)
        {
            var result = new H9ParseResult();

            try
            {
                // Read the stream content first to handle encoding issues
                string content;
                using (var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true))
                {
                    content = reader.ReadToEnd();
                }

                // Remove BOM if present
                if (content.Length > 0 && content[0] == '\uFEFF')
                {
                    content = content.Substring(1);
                }

                // Trim any leading whitespace
                content = content.TrimStart();

                // Always parse as EGF text format
                return ParseEgfTextFormat(content);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Error reading file: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Parse EGF text format tournament file
        /// </summary>
        private H9ParseResult ParseEgfTextFormat(string content)
        {
            var result = new H9ParseResult();

            try
            {
                var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                
                // Parse header lines (start with ;)
                foreach (var line in lines.Where(l => l.TrimStart().StartsWith(";")))
                {
                    var trimmed = line.TrimStart().TrimStart(';').Trim();
                    
                    // Parse EV[] - Event name
                    var evMatch = Regex.Match(trimmed, @"EV\[([^\]]*)\]");
                    if (evMatch.Success)
                        result.TournamentName = evMatch.Groups[1].Value;
                    
                    // Parse PC[] - Place
                    var pcMatch = Regex.Match(trimmed, @"PC\[([^\]]*)\]");
                    if (pcMatch.Success)
                        result.Location = pcMatch.Groups[1].Value;
                    
                    // Parse DT[] - Dates
                    var dtMatch = Regex.Match(trimmed, @"DT\[([^\]]*)\]");
                    if (dtMatch.Success)
                    {
                        var dates = dtMatch.Groups[1].Value.Split(',');
                        if (dates.Length > 0 && DateTime.TryParse(dates[0].Trim(), out var startDate))
                            result.StartDate = startDate;
                        if (dates.Length > 1 && DateTime.TryParse(dates[1].Trim(), out var endDate))
                            result.EndDate = endDate;
                    }
                }

                // Parse player lines - format:
                // Pos Name                    Rank CC Club  Pos2 Pts SOS SODOS R1   R2   R3...  |PIN
                // 1 Matoh Leon              5d SI NMe   1  8   0   0   7+   5+   3+   2+   6+  |10398608
                
                var playerLines = lines.Where(l => !l.TrimStart().StartsWith(";") && Regex.IsMatch(l.Trim(), @"^\d+\s+")).ToList();
                
                // First pass: extract all players
                foreach (var line in playerLines)
                {
                    var player = ParseEgfPlayerLine(line, result.Players.Count);
                    if (player != null)
                    {
                        result.Players.Add(player);
                    }
                }

                // Second pass: extract games from round results
                int maxRounds = 0;
                var addedGames = new HashSet<string>(); // Track added games to avoid duplicates
                
                for (int playerIndex = 0; playerIndex < result.Players.Count; playerIndex++)
                {
                    var line = playerLines[playerIndex];
                    var roundResults = ExtractRoundResults(line);
                    maxRounds = Math.Max(maxRounds, roundResults.Count);

                    for (int roundIndex = 0; roundIndex < roundResults.Count; roundIndex++)
                    {
                        var (opponentNumber, resultChar) = roundResults[roundIndex];
                        
                        // Handle byes (opponent number 0) - create game with BYE_INDEX
                        if (opponentNumber == 0)
                        {
                            // BYE: player wins/loses against no opponent
                            result.Games.Add(new H9Game
                            {
                                RoundNumber = roundIndex + 1,
                                TableNumber = result.Games.Count(g => g.RoundNumber == roundIndex + 1) + 1,
                                WhitePlayerIndex = playerIndex,
                                BlackPlayerIndex = BYE_INDEX,  // -1 indicates BYE
                                Result = resultChar == '+' ? "RESULT_WHITEWINS" : 
                                        resultChar == '-' ? "RESULT_BLACKWINS" : "RESULT_DRAW",
                                IsBye = true
                            });
                            continue;
                        }
                        
                        // Only record game once (when current player index < opponent index)
                        // opponentNumber is 1-based position from the file
                        int opponentIndex = opponentNumber - 1;
                        
                        // Create unique key for this game to avoid duplicates
                        var gameKey = $"{roundIndex}_{Math.Min(playerIndex, opponentIndex)}_{Math.Max(playerIndex, opponentIndex)}";
                        if (addedGames.Contains(gameKey))
                            continue;
                        
                        if (opponentIndex >= 0 && opponentIndex < result.Players.Count)
                        {
                            addedGames.Add(gameKey);
                            
                            string gameResult;
                            int whiteIndex, blackIndex;
                            
                            // Determine who was white/black (lower number usually white)
                            if (playerIndex < opponentIndex)
                            {
                                whiteIndex = playerIndex;
                                blackIndex = opponentIndex;
                                gameResult = resultChar == '+' ? "RESULT_WHITEWINS" : 
                                            resultChar == '-' ? "RESULT_BLACKWINS" : "RESULT_DRAW";
                            }
                            else
                            {
                                whiteIndex = opponentIndex;
                                blackIndex = playerIndex;
                                gameResult = resultChar == '+' ? "RESULT_BLACKWINS" : 
                                            resultChar == '-' ? "RESULT_WHITEWINS" : "RESULT_DRAW";
                            }

                            result.Games.Add(new H9Game
                            {
                                RoundNumber = roundIndex + 1,
                                TableNumber = result.Games.Count(g => g.RoundNumber == roundIndex + 1) + 1,
                                WhitePlayerIndex = whiteIndex,
                                BlackPlayerIndex = blackIndex,
                                Result = gameResult
                            });
                        }
                    }
                }

                result.NumberOfRounds = maxRounds;
                result.Success = result.Players.Any() && result.Games.Any();
                
                if (!result.Success)
                {
                    if (!result.Players.Any())
                        result.ErrorMessage = "No players found in the file";
                    else
                        result.ErrorMessage = "No games found in the file";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Error parsing EGF format: {ex.Message}";
            }

            return result;
        }

        private H9Player ParseEgfPlayerLine(string line, int index)
        {
            try
            {
                // Format: "  1 Matoh Leon              5d SI NMe   1  8   0   0   7+   5+..."
                var trimmed = line.Trim();
                
                // Extract position number at start
                var posMatch = Regex.Match(trimmed, @"^(\d+)\s+");
                if (!posMatch.Success) return null;
                
                var rest = trimmed.Substring(posMatch.Length);
                
                // Find rank pattern (e.g., "5d", "3k", "12k", "1p")
                var rankMatch = Regex.Match(rest, @"\s+(\d{1,2}[dkpDKP])\s+");
                if (!rankMatch.Success)
                {
                    // Try alternate format with just numbers for kyu (like "12" for 12k)
                    rankMatch = Regex.Match(rest, @"\s+(\d{1,2})\s+[A-Z]{2}\s+");
                }
                
                if (!rankMatch.Success) return null;
                
                var name = rest.Substring(0, rankMatch.Index).Trim();
                var afterRank = rest.Substring(rankMatch.Index + rankMatch.Length);
                
                // Extract rank
                var rank = rankMatch.Groups[1].Value.ToUpper();
                if (!rank.EndsWith("D") && !rank.EndsWith("K") && !rank.EndsWith("P"))
                {
                    // Assume kyu if just a number
                    rank += "K";
                }
                
                // Extract country code (2 letters)
                var countryMatch = Regex.Match(afterRank, @"^([A-Z]{2})\s+");
                var country = countryMatch.Success ? countryMatch.Groups[1].Value : "";
                
                // Extract club (next word after country)
                var clubMatch = Regex.Match(afterRank, @"^[A-Z]{2}\s+(\w+)");
                var club = clubMatch.Success ? clubMatch.Groups[1].Value : "";

                return new H9Player
                {
                    PlayerIndex = index,
                    Name = name,
                    FirstName = "", // EGF format has full name in one field
                    Rank = rank,
                    Country = country,
                    Club = club
                };
            }
            catch
            {
                return null;
            }
        }

        private List<(int opponentNumber, char result)> ExtractRoundResults(string line)
        {
            var results = new List<(int, char)>();
            
            // Find all patterns like "7+", "5-", "0=", "12+" etc.
            // These are opponent number followed by +/-/= followed by whitespace or end/pipe
            // Note: \b doesn't work after +/-/= because both are non-word characters
            var matches = Regex.Matches(line, @"(\d+)([+\-=])(?=\s|$|\|)");
            
            foreach (Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, out var opponentNum))
                {
                    // Include ALL results including byes (0+) to preserve round positions
                    // Byes will be skipped when creating games, but round index stays correct
                    results.Add((opponentNum, match.Groups[2].Value[0]));
                }
            }
            
            return results;
        }

        /// <summary>
        /// Get player by index, handling -1 (BYE) case
        /// </summary>
        public H9Player GetPlayer(H9ParseResult parseResult, int index)
        {
            if (index < 0 || index >= parseResult.Players.Count)
                return null;
            return parseResult.Players[index];
        }
    }
}

