using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PlayerRatings.ViewModels.Tournament
{
    /// <summary>
    /// ViewModel for uploading an H9 tournament file
    /// </summary>
    public class ImportH9UploadViewModel
    {
        public Guid TournamentId { get; set; }
        public string TournamentName { get; set; }
        public Guid LeagueId { get; set; }

        [Required(ErrorMessage = "Please select an H9/EGF file")]
        [Display(Name = "Tournament File")]
        public IFormFile H9File { get; set; }
    }

    /// <summary>
    /// ViewModel for previewing extracted matches before import
    /// </summary>
    public class ImportPreviewViewModel
    {
        public Guid TournamentId { get; set; }
        public string TournamentName { get; set; }
        public Guid LeagueId { get; set; }
        public bool HasErrors { get; set; }
        public string ErrorMessage { get; set; }
        
        // Tournament info from file
        public int TotalRounds { get; set; }
        public string H9TournamentName { get; set; }
        public DateTimeOffset? H9StartDate { get; set; }
        public string H9Location { get; set; }

        // Player mappings - one entry per player in the file
        public List<PlayerMappingViewModel> PlayerMappings { get; set; } = new List<PlayerMappingViewModel>();
        
        // Available players in the league for dropdown
        public List<PlayerOption> AvailablePlayers { get; set; } = new List<PlayerOption>();

        // Matches extracted from file (reference players by index)
        public List<ExtractedMatchViewModel> Matches { get; set; } = new List<ExtractedMatchViewModel>();
    }

    /// <summary>
    /// Mapping for a single player from the imported file
    /// </summary>
    public class PlayerMappingViewModel
    {
        public int PlayerIndex { get; set; }  // Index in the H9 file (0-based)
        
        // Info from the file
        public string ExtractedName { get; set; }
        public string ExtractedRanking { get; set; }
        public int GamesCount { get; set; }  // How many games this player has
        
        // Mapping options
        public string SelectedPlayerId { get; set; }  // Existing player ID
        public string SuggestedPlayerId { get; set; }
        public string SuggestedPlayerName { get; set; }
        public double MatchConfidence { get; set; }
        
        // Create new player
        public bool CreateNewPlayer { get; set; }
        public string NewPlayerName { get; set; }
        public string NewPlayerRanking { get; set; }
    }

    /// <summary>
    /// ViewModel for a single extracted match
    /// </summary>
    public class ExtractedMatchViewModel
    {
        public int Index { get; set; }
        public int RoundNumber { get; set; }
        public int TableNumber { get; set; }
        
        // Player references (indices into PlayerMappings list)
        public int WhitePlayerIndex { get; set; }
        public int BlackPlayerIndex { get; set; }
        
        // For display
        public string WhitePlayerName { get; set; }
        public string BlackPlayerName { get; set; }
        
        // Score
        public int WhiteScore { get; set; }
        public int BlackScore { get; set; }
        public int Handicap { get; set; }

        // Include in import
        public bool Include { get; set; } = true;
        
        // BYE match (no opponent, will be imported with Factor = 0)
        public bool IsBye { get; set; }
    }

    /// <summary>
    /// Player option for dropdown
    /// </summary>
    public class PlayerOption
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Ranking { get; set; }
        public string SearchName { get; set; } // Lowercase for searching
    }

    /// <summary>
    /// ViewModel for confirming the import
    /// </summary>
    public class ImportConfirmViewModel
    {
        public Guid TournamentId { get; set; }
        public Guid LeagueId { get; set; }
        public DateTimeOffset MatchDate { get; set; }
        public double? Factor { get; set; }

        // Player mappings
        public List<PlayerMappingInput> PlayerMappings { get; set; } = new List<PlayerMappingInput>();
        
        // Matches to import
        public List<MatchToImport> Matches { get; set; } = new List<MatchToImport>();
    }

    /// <summary>
    /// Player mapping input from form
    /// </summary>
    public class PlayerMappingInput
    {
        public int PlayerIndex { get; set; }
        public string SelectedPlayerId { get; set; }
        public bool CreateNewPlayer { get; set; }
        public string NewPlayerName { get; set; }
        public string NewPlayerRanking { get; set; }
    }

    /// <summary>
    /// A single match ready for import
    /// </summary>
    public class MatchToImport
    {
        public int Index { get; set; }
        public int RoundNumber { get; set; }
        public bool Include { get; set; }
        
        // Player references (indices into PlayerMappings)
        public int WhitePlayerIndex { get; set; }
        public int BlackPlayerIndex { get; set; }

        // Score
        public int WhiteScore { get; set; }
        public int BlackScore { get; set; }
        
        // BYE match (no opponent)
        public bool IsBye { get; set; }
    }
}
