using PlayerRatings.Engine.Stats;
using PlayerRatings.Models;
using Xunit;
using FluentAssertions;

namespace PlayerRatings.UnitTests.Engine.Stats
{
    public class WinRateStatTests
    {
        private readonly ApplicationUser _player1;
        private readonly ApplicationUser _player2;

        public WinRateStatTests()
        {
            _player1 = new ApplicationUser
            {
                Id = "1"
            };
            _player2 = new ApplicationUser
            {
                Id = "2"
            };
        }

        [Fact]
        public void OnlyWinsTest()
        {
            // Arrange
            var stat = new WinRateStat();

            // Act - Player1 wins 2, loses 1
            stat.AddMatch(new Match
            {
                FirstPlayerScore = 7,
                SecondPlayerScore = 5,
                FirstPlayer = _player1,
                SecondPlayer = _player2
            });
            stat.AddMatch(new Match
            {
                FirstPlayerScore = 1,
                SecondPlayerScore = 7,
                FirstPlayer = _player1,
                SecondPlayer = _player2
            });
            stat.AddMatch(new Match
            {
                FirstPlayerScore = 7,
                SecondPlayerScore = 5,
                FirstPlayer = _player1,
                SecondPlayer = _player2
            });

            // Assert - Output format is "P01" which gives "66.7%" plus "(wins/total)"
            var result1 = stat.GetResult(_player1);
            var result2 = stat.GetResult(_player2);
            
            // Player 1: 2 wins out of 3 games = 66.7%
            result1.Should().Contain("(2/3)");
            result1.Should().Contain("66");
            
            // Player 2: 1 win out of 3 games = 33.3%
            result2.Should().Contain("(1/3)");
            result2.Should().Contain("33");
        }

        [Fact]
        public void WithDrawTest()
        {
            // Arrange
            var stat = new WinRateStat();

            // Act - 1 win each, 1 draw
            stat.AddMatch(new Match
            {
                FirstPlayerScore = 7,
                SecondPlayerScore = 5,
                FirstPlayer = _player1,
                SecondPlayer = _player2
            });
            stat.AddMatch(new Match
            {
                FirstPlayerScore = 7,
                SecondPlayerScore = 7,  // Draw
                FirstPlayer = _player1,
                SecondPlayer = _player2
            });
            stat.AddMatch(new Match
            {
                FirstPlayerScore = 5,
                SecondPlayerScore = 7,
                FirstPlayer = _player1,
                SecondPlayer = _player2
            });

            // Assert - Each player has 1 win out of 3 games = 33.3%
            var result1 = stat.GetResult(_player1);
            var result2 = stat.GetResult(_player2);
            
            result1.Should().Contain("(1/3)");
            result1.Should().Contain("33");
            
            result2.Should().Contain("(1/3)");
            result2.Should().Contain("33");
        }

        [Fact]
        public void NoMatches_ReturnsEmptyString()
        {
            // Arrange
            var stat = new WinRateStat();
            var player = new ApplicationUser { Id = "unknown" };

            // Act
            var result = stat.GetResult(player);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void SkipsZeroFactorMatches()
        {
            // Arrange
            var stat = new WinRateStat();

            // Act - Add matches with factor 0 (should be ignored)
            stat.AddMatch(new Match
            {
                FirstPlayerScore = 7,
                SecondPlayerScore = 5,
                FirstPlayer = _player1,
                SecondPlayer = _player2,
                Factor = 0
            });
            stat.AddMatch(new Match
            {
                FirstPlayerScore = 7,
                SecondPlayerScore = 5,
                FirstPlayer = _player1,
                SecondPlayer = _player2,
                Factor = 1  // Only this one should count
            });

            // Assert - Only 1 match counted
            var result1 = stat.GetResult(_player1);
            result1.Should().Contain("(1/1)");
        }

        [Fact]
        public void SkipsByeMatches()
        {
            // Arrange
            var stat = new WinRateStat();

            // Act - Add bye match (null opponent)
            stat.AddMatch(new Match
            {
                FirstPlayerScore = 1,
                SecondPlayerScore = 0,
                FirstPlayer = _player1,
                SecondPlayer = null  // Bye
            });

            // Assert - Bye match should be skipped
            var result = stat.GetResult(_player1);
            result.Should().BeEmpty();
        }
    }
}
