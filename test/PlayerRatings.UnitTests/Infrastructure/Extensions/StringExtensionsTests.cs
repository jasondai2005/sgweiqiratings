using System;
using System.Collections.Generic;
using FluentAssertions;
using PlayerRatings.Infrastructure.Extensions;
using Xunit;

namespace PlayerRatings.UnitTests.Infrastructure.Extensions
{
    public class StringExtensionsTests
    {
        #region Truncate Tests

        [Fact]
        public void Truncate_ShortString_ReturnsOriginal()
        {
            // Arrange
            var value = "Hello";

            // Act
            var result = value.Truncate(10);

            // Assert
            result.Should().Be("Hello");
        }

        [Fact]
        public void Truncate_LongString_TruncatesWithEllipsis()
        {
            // Arrange
            var value = "Hello World This Is A Long String";

            // Act
            var result = value.Truncate(10);

            // Assert
            result.Should().Be("Hello W...");
            result.Length.Should().Be(10);
        }

        [Fact]
        public void Truncate_NullString_ReturnsNull()
        {
            // Arrange
            string value = null;

            // Act
            var result = value.Truncate(10);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Truncate_CustomSuffix_UsesSuffix()
        {
            // Arrange
            var value = "Hello World This Is A Long String";

            // Act
            var result = value.Truncate(15, " [more]");

            // Assert
            result.Should().Be("Hello Wo [more]");
        }

        #endregion

        #region ToTitleCase Tests

        [Fact]
        public void ToTitleCase_LowercaseString_ConvertsCorrectly()
        {
            // Arrange
            var value = "hello world";

            // Act
            var result = value.ToTitleCase();

            // Assert
            result.Should().Be("Hello World");
        }

        [Fact]
        public void ToTitleCase_UppercaseString_ConvertsCorrectly()
        {
            // Arrange
            var value = "HELLO WORLD";

            // Act
            var result = value.ToTitleCase();

            // Assert
            result.Should().Be("Hello World");
        }

        #endregion

        #region ParseLinks Tests

        [Fact]
        public void ParseLinks_ValidLinks_ParsesCorrectly()
        {
            // Arrange
            var value = "https://example.com;https://test.com;https://foo.com";

            // Act
            var result = value.ParseLinks();

            // Assert
            result.Should().HaveCount(3);
            result.Should().Contain("https://example.com");
            result.Should().Contain("https://test.com");
            result.Should().Contain("https://foo.com");
        }

        [Fact]
        public void ParseLinks_EmptyString_ReturnsEmptyList()
        {
            // Arrange
            var value = "";

            // Act
            var result = value.ParseLinks();

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void ParseLinks_NullString_ReturnsEmptyList()
        {
            // Arrange
            string value = null;

            // Act
            var result = value.ParseLinks();

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void ParseLinks_LinksWithSpaces_TrimsCorrectly()
        {
            // Arrange
            var value = "  https://example.com  ; https://test.com  ";

            // Act
            var result = value.ParseLinks();

            // Assert
            result.Should().HaveCount(2);
            result[0].Should().Be("https://example.com");
            result[1].Should().Be("https://test.com");
        }

        #endregion

        #region GetDomainFromUrl Tests

        [Fact]
        public void GetDomainFromUrl_ValidUrl_ReturnsDomain()
        {
            // Arrange
            var url = "https://www.example.com/path/to/page";

            // Act
            var result = url.GetDomainFromUrl();

            // Assert
            result.Should().Be("example.com");
        }

        [Fact]
        public void GetDomainFromUrl_UrlWithoutWww_ReturnsDomain()
        {
            // Arrange
            var url = "https://example.com/path";

            // Act
            var result = url.GetDomainFromUrl();

            // Assert
            result.Should().Be("example.com");
        }

        [Fact]
        public void GetDomainFromUrl_InvalidUrl_ReturnsTruncatedOriginal()
        {
            // Arrange
            var url = "not a valid url";

            // Act
            var result = url.GetDomainFromUrl();

            // Assert
            result.Should().Be("not a valid url");
        }

        #endregion

        #region EnsureProtocol Tests

        [Fact]
        public void EnsureProtocol_UrlWithoutProtocol_AddsHttps()
        {
            // Arrange
            var url = "example.com";

            // Act
            var result = url.EnsureProtocol();

            // Assert
            result.Should().Be("https://example.com");
        }

        [Fact]
        public void EnsureProtocol_UrlWithHttp_ReturnsUnchanged()
        {
            // Arrange
            var url = "http://example.com";

            // Act
            var result = url.EnsureProtocol();

            // Assert
            result.Should().Be("http://example.com");
        }

        [Fact]
        public void EnsureProtocol_UrlWithHttps_ReturnsUnchanged()
        {
            // Arrange
            var url = "https://example.com";

            // Act
            var result = url.EnsureProtocol();

            // Assert
            result.Should().Be("https://example.com");
        }

        #endregion

        #region ToOrdinal Tests

        [Theory]
        [InlineData(1, "1st")]
        [InlineData(2, "2nd")]
        [InlineData(3, "3rd")]
        [InlineData(4, "4th")]
        [InlineData(11, "11th")]
        [InlineData(12, "12th")]
        [InlineData(13, "13th")]
        [InlineData(21, "21st")]
        [InlineData(22, "22nd")]
        [InlineData(23, "23rd")]
        [InlineData(101, "101st")]
        [InlineData(111, "111th")]
        public void ToOrdinal_ReturnsCorrectSuffix(int number, string expected)
        {
            // Act
            var result = number.ToOrdinal();

            // Assert
            result.Should().Be(expected);
        }

        #endregion

        #region ParseOrdinal Tests

        [Theory]
        [InlineData("1st", 1)]
        [InlineData("2nd", 2)]
        [InlineData("3rd", 3)]
        [InlineData("4th", 4)]
        [InlineData("21st", 21)]
        [InlineData("42", 42)]
        public void ParseOrdinal_ValidOrdinal_ReturnsNumber(string ordinal, int expected)
        {
            // Act
            var result = ordinal.ParseOrdinal();

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void ParseOrdinal_InvalidString_ReturnsNull()
        {
            // Act
            var result = "not a number".ParseOrdinal();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void ParseOrdinal_EmptyString_ReturnsNull()
        {
            // Act
            var result = "".ParseOrdinal();

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region ToGradeDisplay Tests

        [Theory]
        [InlineData(2750, "7d")]
        [InlineData(2650, "6d")]
        [InlineData(2100, "1d")]
        [InlineData(2050, "1k")]
        [InlineData(1950, "2k")]
        [InlineData(1000, "11k")]
        [InlineData(50, "21k+")]
        public void ToGradeDisplay_ReturnsCorrectGrade(double rating, string expected)
        {
            // Act
            var result = rating.ToGradeDisplay();

            // Assert
            result.Should().Be(expected);
        }

        #endregion
    }
}

