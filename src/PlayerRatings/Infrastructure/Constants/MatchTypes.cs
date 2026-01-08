namespace PlayerRatings.Infrastructure.Constants
{
    /// <summary>
    /// Constants for match types and tournament identifiers.
    /// </summary>
    public static class MatchTypes
    {
        /// <summary>
        /// Singapore Weiqi Association prefix for match names and tournaments.
        /// </summary>
        public const string SWA = "SWA ";
        
        /// <summary>
        /// Taiwan Go Association prefix.
        /// </summary>
        public const string TGA = "TGA";
        
        /// <summary>
        /// Singapore prefix for general matches.
        /// </summary>
        public const string SG = "SG";
    }
    
    /// <summary>
    /// Constants for tournament types.
    /// </summary>
    public static class TournamentTypes
    {
        /// <summary>
        /// Standard tournament type.
        /// </summary>
        public const string Standard = "Standard";
        
        /// <summary>
        /// Swiss-system tournament type.
        /// </summary>
        public const string Swiss = "Swiss";
        
        /// <summary>
        /// Title/championship tournament type.
        /// </summary>
        public const string Title = "Title";
        
        /// <summary>
        /// League/round-robin tournament type.
        /// </summary>
        public const string League = "League";
    }
    
    /// <summary>
    /// Constants for ranking organizations.
    /// </summary>
    public static class RankingOrganizations
    {
        /// <summary>
        /// Singapore Weiqi Association.
        /// </summary>
        public const string SWA = "SWA";
        
        /// <summary>
        /// Chinese Weiqi Association.
        /// </summary>
        public const string CWA = "CWA";
        
        /// <summary>
        /// Korea Baduk Association.
        /// </summary>
        public const string KBA = "KBA";
        
        /// <summary>
        /// Japan Go Association (Nihon Ki-in).
        /// </summary>
        public const string JGA = "JGA";
        
        /// <summary>
        /// American Go Association.
        /// </summary>
        public const string AGA = "AGA";
        
        /// <summary>
        /// European Go Federation.
        /// </summary>
        public const string EGF = "EGF";
    }
    
    /// <summary>
    /// File upload constants.
    /// </summary>
    public static class FileUpload
    {
        /// <summary>
        /// Maximum file size in bytes (5 MB).
        /// </summary>
        public const long MaxFileSizeBytes = 5 * 1024 * 1024;
        
        /// <summary>
        /// Maximum file size in MB for display.
        /// </summary>
        public const int MaxFileSizeMB = 5;
        
        /// <summary>
        /// Allowed MIME types for image uploads.
        /// </summary>
        public static readonly string[] ImageMimeTypes = new[]
        {
            "image/jpeg",
            "image/png",
            "image/gif",
            "image/webp"
        };
        
        /// <summary>
        /// Allowed MIME types for document uploads.
        /// </summary>
        public static readonly string[] DocumentMimeTypes = new[]
        {
            "application/pdf",
            "text/plain",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
        
        /// <summary>
        /// Allowed file extensions for images.
        /// </summary>
        public static readonly string[] ImageExtensions = new[]
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp"
        };
        
        /// <summary>
        /// Allowed file extensions for documents.
        /// </summary>
        public static readonly string[] DocumentExtensions = new[]
        {
            ".pdf", ".txt", ".xls", ".xlsx"
        };
    }
}

