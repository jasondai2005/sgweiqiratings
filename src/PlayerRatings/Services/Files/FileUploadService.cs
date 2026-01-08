using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace PlayerRatings.Services.Files
{
    /// <summary>
    /// Service for handling file uploads.
    /// </summary>
    public class FileUploadService : IFileUploadService
    {
        private readonly IWebHostEnvironment _env;
        private const string UploadFolder = "uploads";
        
        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private static readonly string[] PdfExtensions = { ".pdf" };
        private static readonly string[] TextExtensions = { ".txt" };
        private static readonly string[] SpreadsheetExtensions = { ".xls", ".xlsx" };

        public FileUploadService(IWebHostEnvironment env)
        {
            _env = env;
        }

        /// <inheritdoc />
        public async Task<FileUploadResult> UploadFileAsync(IFormFile file, FileUploadOptions options)
        {
            // Validate file
            var validationError = ValidateFile(file, options);
            if (!string.IsNullOrEmpty(validationError))
            {
                return FileUploadResult.Fail(validationError);
            }

            try
            {
                // Generate filename
                var filename = options.GenerateUniqueFilename
                    ? GenerateUniqueFilename(file.FileName)
                    : SanitizeFilename(file.FileName);

                // Build paths
                var relativePath = string.IsNullOrEmpty(options.Subdirectory)
                    ? Path.Combine(UploadFolder, filename)
                    : Path.Combine(UploadFolder, options.Subdirectory, filename);
                
                var fullPath = Path.Combine(_env.WebRootPath, relativePath);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Save file
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Return web-friendly path (forward slashes)
                var webPath = "/" + relativePath.Replace("\\", "/");
                return FileUploadResult.Ok(webPath, fullPath);
            }
            catch (Exception ex)
            {
                return FileUploadResult.Fail($"Failed to upload file: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public string ValidateFile(IFormFile file, FileUploadOptions options)
        {
            if (file == null || file.Length == 0)
            {
                return "No file provided or file is empty.";
            }

            // Check file size
            if (file.Length > options.MaxFileSizeBytes)
            {
                var maxSizeMB = options.MaxFileSizeBytes / (1024 * 1024);
                return $"File size exceeds the maximum allowed size of {maxSizeMB} MB.";
            }

            // Check MIME type
            if (options.AllowedMimeTypes.Length > 0)
            {
                var contentType = file.ContentType?.ToLowerInvariant() ?? "";
                if (!options.AllowedMimeTypes.Any(t => t.Equals(contentType, StringComparison.OrdinalIgnoreCase)))
                {
                    // Fallback: check by extension if MIME type detection failed
                    var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "";
                    if (options.AllowedExtensions.Length == 0 || 
                        !options.AllowedExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                    {
                        return "File type is not allowed.";
                    }
                }
            }

            // Check extension
            if (options.AllowedExtensions.Length > 0)
            {
                var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "";
                if (!options.AllowedExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                {
                    return $"File extension '{ext}' is not allowed.";
                }
            }

            return null; // Valid
        }

        /// <inheritdoc />
        public bool DeleteFile(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return false;

            try
            {
                var fullPath = GetFullPath(relativePath);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc />
        public string GetFullPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return null;

            // Remove leading slash if present
            var cleanPath = relativePath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString());
            return Path.Combine(_env.WebRootPath, cleanPath);
        }

        /// <inheritdoc />
        public bool IsImageFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            var ext = Path.GetExtension(path)?.ToLowerInvariant() ?? "";
            return ImageExtensions.Contains(ext);
        }

        /// <inheritdoc />
        public string GetFileIcon(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "📄";

            var ext = Path.GetExtension(path)?.ToLowerInvariant() ?? "";

            if (ImageExtensions.Contains(ext))
                return "🖼️";
            if (PdfExtensions.Contains(ext))
                return "📄";
            if (TextExtensions.Contains(ext))
                return "📝";
            if (SpreadsheetExtensions.Contains(ext))
                return "📊";

            return "📄";
        }

        /// <summary>
        /// Generates a unique filename by prepending a GUID.
        /// </summary>
        private static string GenerateUniqueFilename(string originalFilename)
        {
            var ext = Path.GetExtension(originalFilename);
            var safeName = SanitizeFilename(Path.GetFileNameWithoutExtension(originalFilename));
            return $"{Guid.NewGuid():N}_{safeName}{ext}";
        }

        /// <summary>
        /// Removes unsafe characters from a filename.
        /// </summary>
        private static string SanitizeFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return "file";

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(filename.Where(c => !invalidChars.Contains(c)).ToArray());
            
            // Replace spaces with underscores
            sanitized = sanitized.Replace(" ", "_");
            
            // Ensure filename is not empty after sanitization
            if (string.IsNullOrWhiteSpace(sanitized))
                return "file";

            // Limit length
            if (sanitized.Length > 50)
                sanitized = sanitized.Substring(0, 50);

            return sanitized;
        }
    }
}

