using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PlayerRatings.Infrastructure.Extensions
{
    /// <summary>
    /// Extension methods for string manipulation.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Truncates a string to the specified length, adding ellipsis if truncated.
        /// </summary>
        public static string Truncate(this string value, int maxLength, string suffix = "...")
        {
            if (string.IsNullOrEmpty(value))
                return value;
            
            if (value.Length <= maxLength)
                return value;
            
            return value.Substring(0, maxLength - suffix.Length) + suffix;
        }

        /// <summary>
        /// Converts a string to title case.
        /// </summary>
        public static string ToTitleCase(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLower());
        }

        /// <summary>
        /// Parses a semicolon-separated list of URLs.
        /// </summary>
        public static List<string> ParseLinks(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return new List<string>();
            
            return value.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        /// <summary>
        /// Extracts the domain name from a URL.
        /// </summary>
        public static string GetDomainFromUrl(this string url)
        {
            if (string.IsNullOrEmpty(url))
                return string.Empty;
            
            try
            {
                var uri = new Uri(url);
                return uri.Host.Replace("www.", "");
            }
            catch
            {
                return url.Truncate(30);
            }
        }

        /// <summary>
        /// Ensures a URL has a protocol prefix.
        /// </summary>
        public static string EnsureProtocol(this string url, string defaultProtocol = "https://")
        {
            if (string.IsNullOrEmpty(url))
                return url;
            
            if (url.StartsWith("http://") || url.StartsWith("https://"))
                return url;
            
            return defaultProtocol + url;
        }

        /// <summary>
        /// Converts a rating to a display-friendly grade/rank string.
        /// </summary>
        public static string ToGradeDisplay(this double rating)
        {
            // Standard Go ranking thresholds
            if (rating >= 2700) return "7d";
            if (rating >= 2600) return "6d";
            if (rating >= 2500) return "5d";
            if (rating >= 2400) return "4d";
            if (rating >= 2300) return "3d";
            if (rating >= 2200) return "2d";
            if (rating >= 2100) return "1d";
            if (rating >= 2000) return "1k";
            if (rating >= 1900) return "2k";
            if (rating >= 1800) return "3k";
            if (rating >= 1700) return "4k";
            if (rating >= 1600) return "5k";
            if (rating >= 1500) return "6k";
            if (rating >= 1400) return "7k";
            if (rating >= 1300) return "8k";
            if (rating >= 1200) return "9k";
            if (rating >= 1100) return "10k";
            if (rating >= 1000) return "11k";
            if (rating >= 900) return "12k";
            if (rating >= 800) return "13k";
            if (rating >= 700) return "14k";
            if (rating >= 600) return "15k";
            if (rating >= 500) return "16k";
            if (rating >= 400) return "17k";
            if (rating >= 300) return "18k";
            if (rating >= 200) return "19k";
            if (rating >= 100) return "20k";
            return "21k+";
        }

        /// <summary>
        /// Formats a number with ordinal suffix (1st, 2nd, 3rd, etc.).
        /// </summary>
        public static string ToOrdinal(this int number)
        {
            if (number <= 0)
                return number.ToString();
            
            switch (number % 100)
            {
                case 11:
                case 12:
                case 13:
                    return number + "th";
            }
            
            switch (number % 10)
            {
                case 1: return number + "st";
                case 2: return number + "nd";
                case 3: return number + "rd";
                default: return number + "th";
            }
        }

        /// <summary>
        /// Converts an ordinal string (1st, 2nd, etc.) to a number.
        /// Returns null if parsing fails.
        /// </summary>
        public static int? ParseOrdinal(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;
            
            // Remove ordinal suffixes
            var cleaned = Regex.Replace(value.Trim(), @"(st|nd|rd|th)$", "", RegexOptions.IgnoreCase);
            
            if (int.TryParse(cleaned, out int result))
                return result;
            
            return null;
        }

        /// <summary>
        /// Sanitizes a string for use in HTML output.
        /// </summary>
        public static string HtmlEncode(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            
            return System.Net.WebUtility.HtmlEncode(value);
        }

        /// <summary>
        /// Converts newlines to HTML line breaks.
        /// </summary>
        public static string NewLinesToBr(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            
            return value.Replace("\r\n", "<br>").Replace("\n", "<br>");
        }
    }
}

