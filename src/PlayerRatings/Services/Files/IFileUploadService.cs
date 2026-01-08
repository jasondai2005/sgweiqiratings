using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace PlayerRatings.Services.Files
{
    /// <summary>
    /// Result of a file upload operation.
    /// </summary>
    public class FileUploadResult
    {
        /// <summary>
        /// Whether the upload was successful.
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// The relative path to the uploaded file (for storage in database).
        /// </summary>
        public string RelativePath { get; set; }
        
        /// <summary>
        /// The full path to the uploaded file on disk.
        /// </summary>
        public string FullPath { get; set; }
        
        /// <summary>
        /// Error message if upload failed.
        /// </summary>
        public string ErrorMessage { get; set; }
        
        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static FileUploadResult Ok(string relativePath, string fullPath) => new FileUploadResult
        {
            Success = true,
            RelativePath = relativePath,
            FullPath = fullPath
        };
        
        /// <summary>
        /// Creates a failed result.
        /// </summary>
        public static FileUploadResult Fail(string errorMessage) => new FileUploadResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
    
    /// <summary>
    /// Options for file upload validation.
    /// </summary>
    public class FileUploadOptions
    {
        /// <summary>
        /// Maximum file size in bytes. Default is 5 MB.
        /// </summary>
        public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
        
        /// <summary>
        /// Allowed MIME types. If empty, all types are allowed.
        /// </summary>
        public string[] AllowedMimeTypes { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// Allowed file extensions (including dot). If empty, all extensions are allowed.
        /// </summary>
        public string[] AllowedExtensions { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// Subdirectory within uploads folder.
        /// </summary>
        public string Subdirectory { get; set; } = "";
        
        /// <summary>
        /// Whether to generate a unique filename. Default is true.
        /// </summary>
        public bool GenerateUniqueFilename { get; set; } = true;
        
        /// <summary>
        /// Predefined options for image uploads.
        /// </summary>
        public static FileUploadOptions ImageUpload => new FileUploadOptions
        {
            AllowedMimeTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" },
            AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }
        };
        
        /// <summary>
        /// Predefined options for tournament photo uploads.
        /// </summary>
        public static FileUploadOptions TournamentPhoto => new FileUploadOptions
        {
            AllowedMimeTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" },
            AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" },
            Subdirectory = "tournaments"
        };
        
        /// <summary>
        /// Predefined options for standings file uploads (images + documents).
        /// </summary>
        public static FileUploadOptions StandingsFile => new FileUploadOptions
        {
            AllowedMimeTypes = new[] 
            { 
                "image/jpeg", "image/png", "image/gif", "image/webp",
                "application/pdf", "text/plain",
                "application/vnd.ms-excel", 
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            },
            AllowedExtensions = new[] 
            { 
                ".jpg", ".jpeg", ".png", ".gif", ".webp",
                ".pdf", ".txt", ".xls", ".xlsx"
            },
            Subdirectory = "standings"
        };
        
        /// <summary>
        /// Predefined options for player photo uploads.
        /// </summary>
        public static FileUploadOptions PlayerPhoto => new FileUploadOptions
        {
            AllowedMimeTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" },
            AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" },
            Subdirectory = "players"
        };
    }
    
    /// <summary>
    /// Service for handling file uploads.
    /// </summary>
    public interface IFileUploadService
    {
        /// <summary>
        /// Uploads a file with validation.
        /// </summary>
        /// <param name="file">The file to upload.</param>
        /// <param name="options">Upload options for validation.</param>
        /// <returns>Upload result with path or error message.</returns>
        Task<FileUploadResult> UploadFileAsync(IFormFile file, FileUploadOptions options);
        
        /// <summary>
        /// Validates a file without uploading it.
        /// </summary>
        /// <param name="file">The file to validate.</param>
        /// <param name="options">Validation options.</param>
        /// <returns>Null if valid, error message if invalid.</returns>
        string ValidateFile(IFormFile file, FileUploadOptions options);
        
        /// <summary>
        /// Deletes a previously uploaded file.
        /// </summary>
        /// <param name="relativePath">The relative path returned from upload.</param>
        /// <returns>True if deleted successfully.</returns>
        bool DeleteFile(string relativePath);
        
        /// <summary>
        /// Gets the full path for a relative upload path.
        /// </summary>
        /// <param name="relativePath">The relative path.</param>
        /// <returns>Full path on disk.</returns>
        string GetFullPath(string relativePath);
        
        /// <summary>
        /// Checks if a file is an image based on extension.
        /// </summary>
        /// <param name="path">File path or name.</param>
        /// <returns>True if the file is an image.</returns>
        bool IsImageFile(string path);
        
        /// <summary>
        /// Gets the appropriate icon for a file type.
        /// </summary>
        /// <param name="path">File path or name.</param>
        /// <returns>Icon string (emoji).</returns>
        string GetFileIcon(string path);
    }
}

