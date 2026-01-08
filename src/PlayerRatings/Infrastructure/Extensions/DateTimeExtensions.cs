using System;
using System.Globalization;

namespace PlayerRatings.Infrastructure.Extensions
{
    /// <summary>
    /// Extension methods for DateTime and DateTimeOffset formatting and manipulation.
    /// </summary>
    public static class DateTimeExtensions
    {
        /// <summary>
        /// Formats a date for display (e.g., "Jan 15, 2024").
        /// </summary>
        public static string ToDisplayDate(this DateTimeOffset date)
            => date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
        
        /// <summary>
        /// Formats a date for display (e.g., "Jan 15, 2024").
        /// </summary>
        public static string ToDisplayDate(this DateTimeOffset? date)
            => date?.ToDisplayDate() ?? string.Empty;
        
        /// <summary>
        /// Formats a date in short format (e.g., "15 Jan 2024").
        /// </summary>
        public static string ToShortDisplayDate(this DateTimeOffset date)
            => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        
        /// <summary>
        /// Formats a date for ISO format (e.g., "2024-01-15").
        /// </summary>
        public static string ToIsoDate(this DateTimeOffset date)
            => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        
        /// <summary>
        /// Formats a date and time for display (e.g., "Jan 15, 2024 14:30").
        /// </summary>
        public static string ToDisplayDateTime(this DateTimeOffset date)
            => date.ToString("MMM d, yyyy HH:mm", CultureInfo.InvariantCulture);
        
        /// <summary>
        /// Gets the last second of the month for the given date.
        /// E.g., Jan 15, 2024 → Jan 31, 2024 23:59:59
        /// </summary>
        public static DateTimeOffset GetEndOfMonth(this DateTimeOffset date)
            => new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, date.Offset).AddMonths(1).AddSeconds(-1);
        
        /// <summary>
        /// Gets the first moment of the month for the given date.
        /// E.g., Jan 15, 2024 → Jan 1, 2024 00:00:00
        /// </summary>
        public static DateTimeOffset GetStartOfMonth(this DateTimeOffset date)
            => new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, date.Offset);
        
        /// <summary>
        /// Gets the last second of the day for the given date.
        /// </summary>
        public static DateTimeOffset GetEndOfDay(this DateTimeOffset date)
            => date.Date.AddDays(1).AddSeconds(-1);
        
        /// <summary>
        /// Checks if a date is within a date range (inclusive).
        /// </summary>
        public static bool IsBetween(this DateTimeOffset date, DateTimeOffset start, DateTimeOffset end)
            => date >= start && date <= end;
        
        /// <summary>
        /// Gets a display-friendly "time ago" string.
        /// </summary>
        public static string ToTimeAgo(this DateTimeOffset date)
        {
            var span = DateTimeOffset.UtcNow - date;
            
            if (span.TotalDays > 365)
                return $"{(int)(span.TotalDays / 365)} year{((int)(span.TotalDays / 365) != 1 ? "s" : "")} ago";
            if (span.TotalDays > 30)
                return $"{(int)(span.TotalDays / 30)} month{((int)(span.TotalDays / 30) != 1 ? "s" : "")} ago";
            if (span.TotalDays > 1)
                return $"{(int)span.TotalDays} day{((int)span.TotalDays != 1 ? "s" : "")} ago";
            if (span.TotalHours > 1)
                return $"{(int)span.TotalHours} hour{((int)span.TotalHours != 1 ? "s" : "")} ago";
            if (span.TotalMinutes > 1)
                return $"{(int)span.TotalMinutes} minute{((int)span.TotalMinutes != 1 ? "s" : "")} ago";
            
            return "just now";
        }
        
        /// <summary>
        /// Formats a date range for display.
        /// </summary>
        public static string FormatDateRange(DateTimeOffset? start, DateTimeOffset? end)
        {
            if (!start.HasValue && !end.HasValue)
                return string.Empty;
            
            if (!end.HasValue || (start.HasValue && start.Value.Date == end.Value.Date))
                return start?.ToDisplayDate() ?? string.Empty;
            
            if (!start.HasValue)
                return $"Until {end.Value.ToDisplayDate()}";
            
            // Same month and year
            if (start.Value.Year == end.Value.Year && start.Value.Month == end.Value.Month)
                return $"{start.Value:MMM d}-{end.Value:d}, {end.Value:yyyy}";
            
            // Same year
            if (start.Value.Year == end.Value.Year)
                return $"{start.Value:MMM d} - {end.Value:MMM d}, {end.Value:yyyy}";
            
            // Different years
            return $"{start.Value.ToDisplayDate()} - {end.Value.ToDisplayDate()}";
        }
    }
}

