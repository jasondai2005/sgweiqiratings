using System;
using FluentAssertions;
using PlayerRatings.Infrastructure.Extensions;
using Xunit;

namespace PlayerRatings.UnitTests.Infrastructure.Extensions
{
    public class DateTimeExtensionsTests
    {
        [Fact]
        public void ToDisplayDate_FormatsCorrectly()
        {
            // Arrange
            var date = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

            // Act
            var result = date.ToDisplayDate();

            // Assert
            result.Should().Be("Jan 15, 2024");
        }

        [Fact]
        public void ToDisplayDate_NullableWithValue_FormatsCorrectly()
        {
            // Arrange
            DateTimeOffset? date = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

            // Act
            var result = date.ToDisplayDate();

            // Assert
            result.Should().Be("Jan 15, 2024");
        }

        [Fact]
        public void ToDisplayDate_NullableWithoutValue_ReturnsEmptyString()
        {
            // Arrange
            DateTimeOffset? date = null;

            // Act
            var result = date.ToDisplayDate();

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void GetEndOfMonth_ReturnsLastSecondOfMonth()
        {
            // Arrange
            var date = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

            // Act
            var result = date.GetEndOfMonth();

            // Assert
            result.Year.Should().Be(2024);
            result.Month.Should().Be(1);
            result.Day.Should().Be(31);
            result.Hour.Should().Be(23);
            result.Minute.Should().Be(59);
            result.Second.Should().Be(59);
        }

        [Fact]
        public void GetEndOfMonth_FebruaryLeapYear_ReturnsCorrectDate()
        {
            // Arrange (2024 is a leap year)
            var date = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero);

            // Act
            var result = date.GetEndOfMonth();

            // Assert
            result.Day.Should().Be(29);
        }

        [Fact]
        public void GetEndOfMonth_FebruaryNonLeapYear_ReturnsCorrectDate()
        {
            // Arrange (2023 is not a leap year)
            var date = new DateTimeOffset(2023, 2, 1, 0, 0, 0, TimeSpan.Zero);

            // Act
            var result = date.GetEndOfMonth();

            // Assert
            result.Day.Should().Be(28);
        }

        [Fact]
        public void GetStartOfMonth_ReturnsFirstMomentOfMonth()
        {
            // Arrange
            var date = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

            // Act
            var result = date.GetStartOfMonth();

            // Assert
            result.Year.Should().Be(2024);
            result.Month.Should().Be(1);
            result.Day.Should().Be(1);
            result.Hour.Should().Be(0);
            result.Minute.Should().Be(0);
            result.Second.Should().Be(0);
        }

        [Fact]
        public void IsBetween_DateInRange_ReturnsTrue()
        {
            // Arrange
            var start = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var end = new DateTimeOffset(2024, 1, 31, 23, 59, 59, TimeSpan.Zero);
            var date = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);

            // Act
            var result = date.IsBetween(start, end);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsBetween_DateOutsideRange_ReturnsFalse()
        {
            // Arrange
            var start = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var end = new DateTimeOffset(2024, 1, 31, 23, 59, 59, TimeSpan.Zero);
            var date = new DateTimeOffset(2024, 2, 15, 0, 0, 0, TimeSpan.Zero);

            // Act
            var result = date.IsBetween(start, end);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsBetween_DateEqualsStart_ReturnsTrue()
        {
            // Arrange
            var start = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var end = new DateTimeOffset(2024, 1, 31, 23, 59, 59, TimeSpan.Zero);
            var date = start;

            // Act
            var result = date.IsBetween(start, end);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void FormatDateRange_BothDates_ReturnsFormattedRange()
        {
            // Arrange
            DateTimeOffset? start = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset? end = new DateTimeOffset(2024, 1, 20, 0, 0, 0, TimeSpan.Zero);

            // Act
            var result = DateTimeExtensions.FormatDateRange(start, end);

            // Assert - format is "MMM d-d, yyyy" for same month
            result.Should().Contain("Jan");
            result.Should().Contain("15");
            result.Should().Contain("20");
            result.Should().Contain("2024");
        }

        [Fact]
        public void FormatDateRange_SameDate_ReturnsSingleDate()
        {
            // Arrange
            DateTimeOffset? start = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset? end = new DateTimeOffset(2024, 1, 15, 23, 59, 59, TimeSpan.Zero);

            // Act
            var result = DateTimeExtensions.FormatDateRange(start, end);

            // Assert
            result.Should().Be("Jan 15, 2024");
        }

        [Fact]
        public void FormatDateRange_DifferentMonths_ReturnsFormattedRange()
        {
            // Arrange
            DateTimeOffset? start = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset? end = new DateTimeOffset(2024, 2, 20, 0, 0, 0, TimeSpan.Zero);

            // Act
            var result = DateTimeExtensions.FormatDateRange(start, end);

            // Assert
            result.Should().Be("Jan 15 - Feb 20, 2024");
        }

        [Fact]
        public void FormatDateRange_DifferentYears_ReturnsFullFormattedRange()
        {
            // Arrange
            DateTimeOffset? start = new DateTimeOffset(2023, 12, 15, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset? end = new DateTimeOffset(2024, 1, 20, 0, 0, 0, TimeSpan.Zero);

            // Act
            var result = DateTimeExtensions.FormatDateRange(start, end);

            // Assert
            result.Should().Be("Dec 15, 2023 - Jan 20, 2024");
        }

        [Fact]
        public void FormatDateRange_NullDates_ReturnsEmptyString()
        {
            // Arrange
            DateTimeOffset? start = null;
            DateTimeOffset? end = null;

            // Act
            var result = DateTimeExtensions.FormatDateRange(start, end);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void ToIsoDate_FormatsCorrectly()
        {
            // Arrange
            var date = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

            // Act
            var result = date.ToIsoDate();

            // Assert
            result.Should().Be("2024-01-15");
        }
    }
}

