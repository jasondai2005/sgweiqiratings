using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PlayerRatings.Services
{
    /// <summary>
    /// Represents a player from an H9 file
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
    /// Represents a game/match from an H9 file
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
    /// Result of parsing an H9 file
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
    /// Parser for OpenGotha H9 tournament files
    /// </summary>
    public class H9FileParser
    {
        /// <summary>
        /// Special index indicating a BYE (no opponent)
        /// </summary>
        public const int BYE_INDEX = -1;
        /// <summary>
        /// Parse an H9 file from a stream. Supports both OpenGotha XML format and EGF text format.
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

                // Detect format: XML (OpenGotha H9) or text (EGF format)
                if (content.StartsWith("<"))
                {
                    return ParseXmlFormat(content);
                }
                else if (content.StartsWith(";") || Regex.IsMatch(content, @"^\s*\d+\s+\w+"))
                {
                    return ParseEgfTextFormat(content);
                }
                else
                {
                    result.Success = false;
                    var preview = content.Length > 100 ? content.Substring(0, 100) : content;
                    result.ErrorMessage = $"Unknown file format. First 100 chars: {preview}";
                    return result;
                }
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
        /// Parse OpenGotha XML format
        /// </summary>
        private H9ParseResult ParseXmlFormat(string content)
        {
            var result = new H9ParseResult();

            try
            {
                // Check if it looks like an H9 file (should contain Tournament root)
                if (!content.Contains("<Tournament") && !content.Contains("<tournament"))
                {
                    result.Success = false;
                    result.ErrorMessage = "XML file does not appear to be an OpenGotha H9 file. Expected <Tournament> root element.";
                    return result;
                }

                var doc = XDocument.Parse(content);
                var root = doc.Root;

                if (root == null || root.Name.LocalName != "Tournament")
                {
                    result.Success = false;
                    result.ErrorMessage = "Invalid H9 file: Root element must be 'Tournament'";
                    return result;
                }

                // Parse tournament parameters
                var paramSet = root.Element("TournamentParameterSet");
                if (paramSet != null)
                {
                    var generalParams = paramSet.Element("GeneralParameterSet");
                    if (generalParams != null)
                    {
                        result.TournamentName = generalParams.Attribute("name")?.Value;
                        result.Location = generalParams.Attribute("location")?.Value;
                        
                        // Parse dates
                        var beginDate = generalParams.Attribute("beginDate")?.Value;
                        var endDate = generalParams.Attribute("endDate")?.Value;
                        
                        if (!string.IsNullOrEmpty(beginDate) && DateTime.TryParse(beginDate, out var bd))
                            result.StartDate = bd;
                        if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var ed))
                            result.EndDate = ed;

                        var nbRounds = generalParams.Attribute("numberOfRounds")?.Value;
                        if (!string.IsNullOrEmpty(nbRounds) && int.TryParse(nbRounds, out var rounds))
                            result.NumberOfRounds = rounds;
                    }
                }

                // Parse players
                var playersElement = root.Element("Players");
                if (playersElement != null)
                {
                    int index = 0;
                    foreach (var playerElement in playersElement.Elements("Player"))
                    {
                        var player = new H9Player
                        {
                            PlayerIndex = index++,
                            Name = playerElement.Attribute("name")?.Value ?? "",
                            FirstName = playerElement.Attribute("firstName")?.Value ?? "",
                            Rank = playerElement.Attribute("rank")?.Value ?? "",
                            Country = playerElement.Attribute("country")?.Value ?? "",
                            Club = playerElement.Attribute("club")?.Value ?? ""
                        };
                        result.Players.Add(player);
                    }
                }

                // Parse games
                var gamesElement = root.Element("Games");
                if (gamesElement != null)
                {
                    foreach (var gameElement in gamesElement.Elements("Game"))
                    {
                        var game = new H9Game();

                        var roundAttr = gameElement.Attribute("roundNumber")?.Value;
                        if (!string.IsNullOrEmpty(roundAttr) && int.TryParse(roundAttr, out var round))
                            game.RoundNumber = round;

                        var tableAttr = gameElement.Attribute("tableNumber")?.Value;
                        if (!string.IsNullOrEmpty(tableAttr) && int.TryParse(tableAttr, out var table))
                            game.TableNumber = table;

                        var whiteAttr = gameElement.Attribute("whitePlayer")?.Value;
                        if (!string.IsNullOrEmpty(whiteAttr) && int.TryParse(whiteAttr, out var white))
                            game.WhitePlayerIndex = white;

                        var blackAttr = gameElement.Attribute("blackPlayer")?.Value;
                        if (!string.IsNullOrEmpty(blackAttr) && int.TryParse(blackAttr, out var black))
                            game.BlackPlayerIndex = black;

                        game.Result = gameElement.Attribute("result")?.Value ?? "";

                        var handicapAttr = gameElement.Attribute("handicap")?.Value;
                        if (!string.IsNullOrEmpty(handicapAttr) && int.TryParse(handicapAttr, out var handicap))
                            game.Handicap = handicap;

                        result.Games.Add(game);
                    }
                }

                result.Success = result.Players.Any() && result.Games.Any();
                if (!result.Success)
                {
                    if (!result.Players.Any() && !result.Games.Any())
                    {
                        result.ErrorMessage = "No players or games found in the H9 file. Root element: " + root?.Name.LocalName;
                    }
                    else if (!result.Players.Any())
                    {
                        result.ErrorMessage = "No players found in the H9 file";
                    }
                    else
                    {
                        result.ErrorMessage = "No games found in the H9 file";
                    }
                }
            }
            catch (System.Xml.XmlException xmlEx)
            {
                result.Success = false;
                result.ErrorMessage = $"Invalid XML format: {xmlEx.Message}";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Error parsing H9 file: {ex.Message}";
            }

            return result;
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

