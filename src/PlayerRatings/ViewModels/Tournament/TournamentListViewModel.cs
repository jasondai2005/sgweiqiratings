using System;
using System.Collections.Generic;

namespace PlayerRatings.ViewModels.Tournament
{
    /// <summary>
    /// View model for the tournament list page
    /// </summary>
    public class TournamentListViewModel
    {
        public Guid LeagueId { get; set; }
        
        public string LeagueName { get; set; }
        
        public bool IsAdmin { get; set; }
        
        public List<TournamentSummary> Tournaments { get; set; } = new List<TournamentSummary>();
        
        // Year navigation properties
        public int? CurrentYear { get; set; }
        
        public int? PreviousYear { get; set; }
        
        public int? NextYear { get; set; }
        
        public List<int> AvailableYears { get; set; } = new List<int>();
        
        public int TotalTournamentCount { get; set; }
    }
    
    /// <summary>
    /// Summary information for a tournament in the list
    /// </summary>
    public class TournamentSummary
    {
        public Guid Id { get; set; }
        
        public string Name { get; set; }
        
        public string Ordinal { get; set; }
        
        public string Group { get; set; }
        
        public string FullName { get; set; }
        
        public string Organizer { get; set; }
        
        public string Location { get; set; }
        
        public DateTimeOffset? StartDate { get; set; }
        
        public DateTimeOffset? EndDate { get; set; }
        
        public string TournamentType { get; set; }
        
        public double? Factor { get; set; }
        
        public int MatchCount { get; set; }
        
        public int PlayerCount { get; set; }
    }
}

