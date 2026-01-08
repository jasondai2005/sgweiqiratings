using FluentAssertions;
using PlayerRatings.Engine.Rating;
using Xunit;

namespace PlayerRatings.UnitTests.Engine.Rating
{
    public class EloTests
    {
        /// <summary>
        /// Tests the Elo rating calculation with tiered K factors.
        /// K factors by rating tier:
        /// - Pro (2720+): K = 6
        /// - 3D+ (2300-2719): K = 12
        /// - 5K-2D (1950-2299): K = 20
        /// - 15K-6K (1600-1949): K = 28
        /// - 16K and below (&lt;1600): K = 36
        /// </summary>
        [Theory]
        // Equal ratings (expected score = 0.5), K varies by tier
        [InlineData(1400, 1400, 1, 0, 18)]    // K=36 for <1600, shift = 36*(1-0.5) = 18
        [InlineData(1700, 1700, 1, 0, 14)]    // K=28 for 1600-1949, shift = 28*(1-0.5) = 14
        [InlineData(2000, 2000, 1, 0, 10)]    // K=20 for 1950-2299, shift = 20*(1-0.5) = 10
        [InlineData(2400, 2400, 1, 0, 6)]     // K=12 for 2300-2719, shift = 12*(1-0.5) = 6
        [InlineData(2800, 2800, 1, 0, 3)]     // K=6 for 2720+, shift = 6*(1-0.5) = 3
        // Draw at equal ratings - no change
        [InlineData(1500, 1500, 0.5, 0.5, 0)]
        [InlineData(2000, 2000, 0.5, 0.5, 0)]
        public void CalculationTest(int playerRatingA, int playerRatingB, double playerAScore, double playerBScore, int expectedShift)
        {
            // Act
            var result = new Elo(playerRatingA, playerRatingB, playerAScore, playerBScore);

            // Assert - use tolerance for floating point comparisons
            result.ShiftRatingAPlayer.Should().BeApproximately(expectedShift, 0.5);
            result.NewRatingAPlayer.Should().BeApproximately(playerRatingA + expectedShift, 0.5);
        }

        [Fact]
        public void GetK_ReturnsCorrectKFactor_ForDifferentRatings()
        {
            // Pro tier (2720+)
            Elo.GetK(2800).Should().Be(6);
            Elo.GetK(2720).Should().Be(6);
            
            // 3D+ tier (2300-2719)
            Elo.GetK(2719).Should().Be(12);
            Elo.GetK(2400).Should().Be(12);
            Elo.GetK(2300).Should().Be(12);
            
            // 5K-2D tier (1950-2299)
            Elo.GetK(2299).Should().Be(20);
            Elo.GetK(2100).Should().Be(20);
            Elo.GetK(1950).Should().Be(20);
            
            // 15K-6K tier (1600-1949)
            Elo.GetK(1949).Should().Be(28);
            Elo.GetK(1700).Should().Be(28);
            Elo.GetK(1600).Should().Be(28);
            
            // 16K and below (<1600)
            Elo.GetK(1599).Should().Be(36);
            Elo.GetK(1400).Should().Be(36);
            Elo.GetK(1000).Should().Be(36);
        }

        [Fact]
        public void Constructor_WithCustomConFactor_UsesProvidedFactor()
        {
            // Arrange
            double customK = 10;
            
            // Act - equal ratings with custom K
            var result = new Elo(1500, 1500, 1, 0, customK);
            
            // Assert - shift should be K * (actual - expected) = 10 * (1 - 0.5) = 5
            result.ShiftRatingAPlayer.Should().BeApproximately(5, 0.01);
        }

        [Fact]
        public void NewRating_HasMinimumOf900()
        {
            // Act - very low rated player loses to much higher rated player
            var result = new Elo(900, 2000, 0, 1);
            
            // Assert - rating should not go below 900
            result.NewRatingAPlayer.Should().BeGreaterOrEqualTo(900);
        }

        [Fact]
        public void ShiftRatings_AreOpposite_ForNonDrawGames()
        {
            // Act
            var result = new Elo(1500, 1600, 1, 0);
            
            // Assert - shifts should be opposite in sign but magnitude may differ due to different K factors
            // For Player A (1500): K = 28, Expected = 0.36, Shift = 28 * (1 - 0.36) = 17.9
            // For Player B (1600): K = 28, Expected = 0.64, Shift = 28 * (0 - 0.64) = -17.9
            result.ShiftRatingAPlayer.Should().BePositive();
            result.ShiftRatingBPlayer.Should().BeNegative();
        }
    }
}
