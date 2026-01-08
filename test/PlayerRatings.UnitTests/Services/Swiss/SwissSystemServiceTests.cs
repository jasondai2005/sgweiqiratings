using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using PlayerRatings.Models;
using PlayerRatings.Services.Swiss;
using Xunit;

namespace PlayerRatings.UnitTests.Services.Swiss
{
    public class SwissSystemServiceTests
    {
        private readonly SwissSystemService _service;

        public SwissSystemServiceTests()
        {
            _service = new SwissSystemService();
        }

        #region CalculateSwissStats Tests

        [Fact]
        public void CalculateSwissStats_EmptyMatches_ReturnsEmptyStats()
        {
            // Arrange
            var matches = new List<Match>();

            // Act
            var result = _service.CalculateSwissStats(matches);

            // Assert
            result.PlayerStats.Should().BeEmpty();
            result.SOS.Should().BeEmpty();
            result.SOSOS.Should().BeEmpty();
        }

        [Fact]
        public void CalculateSwissStats_SingleMatch_CalculatesWinsAndLosses()
        {
            // Arrange
            var player1Id = "player1";
            var player2Id = "player2";
            var matches = new List<Match>
            {
                CreateMatch(player1Id, player2Id, 1, 0) // Player 1 wins
            };

            // Act
            var result = _service.CalculateSwissStats(matches);

            // Assert
            result.PlayerStats.Should().ContainKey(player1Id);
            result.PlayerStats.Should().ContainKey(player2Id);
            result.PlayerStats[player1Id].Wins.Should().Be(1);
            result.PlayerStats[player1Id].Losses.Should().Be(0);
            result.PlayerStats[player2Id].Wins.Should().Be(0);
            result.PlayerStats[player2Id].Losses.Should().Be(1);
        }

        [Fact]
        public void CalculateSwissStats_Draw_AwardsHalfPointToEach()
        {
            // Arrange
            var player1Id = "player1";
            var player2Id = "player2";
            var matches = new List<Match>
            {
                CreateMatch(player1Id, player2Id, 1, 1) // Draw
            };

            // Act
            var result = _service.CalculateSwissStats(matches);

            // Assert
            result.PlayerStats[player1Id].Wins.Should().Be(0.5);
            result.PlayerStats[player1Id].Draws.Should().Be(1);
            result.PlayerStats[player2Id].Wins.Should().Be(0.5);
            result.PlayerStats[player2Id].Draws.Should().Be(1);
        }

        [Fact]
        public void CalculateSwissStats_ByeMatch_CountsAsWinForRealPlayer()
        {
            // Arrange
            var player1Id = "player1";
            var matches = new List<Match>
            {
                CreateMatch(player1Id, null, 1, 0) // Bye win for player1
            };

            // Act
            var result = _service.CalculateSwissStats(matches);

            // Assert
            result.PlayerStats.Should().ContainKey(player1Id);
            result.PlayerStats[player1Id].Wins.Should().Be(1);
            result.PlayerStats[player1Id].Losses.Should().Be(0);
        }

        [Fact]
        public void CalculateSwissStats_TracksOpponents()
        {
            // Arrange
            var player1Id = "player1";
            var player2Id = "player2";
            var player3Id = "player3";
            var matches = new List<Match>
            {
                CreateMatch(player1Id, player2Id, 1, 0),
                CreateMatch(player1Id, player3Id, 1, 0)
            };

            // Act
            var result = _service.CalculateSwissStats(matches);

            // Assert
            result.PlayerStats[player1Id].Opponents.Should().Contain(player2Id);
            result.PlayerStats[player1Id].Opponents.Should().Contain(player3Id);
            result.PlayerStats[player2Id].Opponents.Should().Contain(player1Id);
            result.PlayerStats[player3Id].Opponents.Should().Contain(player1Id);
        }

        [Fact]
        public void CalculateSwissStats_CalculatesSOS()
        {
            // Arrange
            // Player1 beats Player2, Player2 beats Player3
            // Player1's SOS = Player2's wins = 1
            // Player2's SOS = Player1's wins + Player3's wins = 1 + 0 = 1
            // Player3's SOS = Player2's wins = 1
            var player1Id = "player1";
            var player2Id = "player2";
            var player3Id = "player3";
            var matches = new List<Match>
            {
                CreateMatch(player1Id, player2Id, 1, 0),
                CreateMatch(player2Id, player3Id, 1, 0)
            };

            // Act
            var result = _service.CalculateSwissStats(matches);

            // Assert
            result.SOS[player1Id].Should().Be(1); // Player2 has 1 win
            result.SOS[player2Id].Should().Be(1); // Player1 has 1 win, Player3 has 0 wins
            result.SOS[player3Id].Should().Be(1); // Player2 has 1 win
        }

        [Fact]
        public void CalculateSwissStats_TracksPointsForAndAgainst()
        {
            // Arrange
            var player1Id = "player1";
            var player2Id = "player2";
            var matches = new List<Match>
            {
                CreateMatch(player1Id, player2Id, 3, 2)
            };

            // Act
            var result = _service.CalculateSwissStats(matches);

            // Assert
            result.PlayerStats[player1Id].PointsFor.Should().Be(3);
            result.PlayerStats[player1Id].PointsAgainst.Should().Be(2);
            result.PlayerStats[player2Id].PointsFor.Should().Be(2);
            result.PlayerStats[player2Id].PointsAgainst.Should().Be(3);
        }

        #endregion

        #region CalculateSwissPositions Tests

        [Fact]
        public void CalculateSwissPositions_UndefeatedPlayer_GetsPositionOne()
        {
            // Arrange
            var player1Id = "player1";
            var player2Id = "player2";
            var matches = new List<Match>
            {
                CreateMatch(player1Id, player2Id, 1, 0) // Player1 undefeated
            };
            var stats = _service.CalculateSwissStats(matches);

            // Act
            var positions = _service.CalculateSwissPositions(stats);

            // Assert
            positions[player1Id].Should().Be(1);
            positions[player2Id].Should().Be(2);
        }

        [Fact]
        public void CalculateSwissPositions_MultipleUndefeated_AllGetPositionOne()
        {
            // Arrange
            // 4 players, round-robin where player1 and player2 beat their opponents
            // but haven't played each other
            var player1Id = "player1";
            var player2Id = "player2";
            var player3Id = "player3";
            var player4Id = "player4";
            var matches = new List<Match>
            {
                CreateMatch(player1Id, player3Id, 1, 0),
                CreateMatch(player2Id, player4Id, 1, 0),
                CreateMatch(player1Id, player4Id, 1, 0),
                CreateMatch(player2Id, player3Id, 1, 0)
            };
            var stats = _service.CalculateSwissStats(matches);

            // Act
            var positions = _service.CalculateSwissPositions(stats);

            // Assert
            positions[player1Id].Should().Be(1); // 2 wins, 0 losses
            positions[player2Id].Should().Be(1); // 2 wins, 0 losses
        }

        [Fact]
        public void CalculateSwissPositions_OrdersByWinsThenSOS()
        {
            // Arrange
            var player1Id = "player1";
            var player2Id = "player2";
            var player3Id = "player3";
            // Player1 beats Player3
            // Player2 beats Player3
            // Player1 has higher SOS if Player2 has more wins (from beating player1)
            var matches = new List<Match>
            {
                CreateMatch(player1Id, player3Id, 1, 0),
                CreateMatch(player2Id, player3Id, 1, 0),
                CreateMatch(player2Id, player1Id, 1, 0) // Player2 beats Player1
            };
            var stats = _service.CalculateSwissStats(matches);

            // Act
            var positions = _service.CalculateSwissPositions(stats);

            // Assert
            positions[player2Id].Should().Be(1); // 2 wins, undefeated
            // Player1 and Player3 both have losses
            positions[player1Id].Should().BeLessThan(positions[player3Id]); // Player1 has 1 win vs Player3 has 0 wins
        }

        #endregion

        #region Helper Methods

        private static Match CreateMatch(string player1Id, string player2Id, int player1Score, int player2Score)
        {
            return new Match
            {
                Id = Guid.NewGuid(),
                FirstPlayerId = player1Id,
                SecondPlayerId = player2Id,
                FirstPlayerScore = player1Score,
                SecondPlayerScore = player2Score,
                Date = DateTimeOffset.UtcNow
            };
        }

        #endregion
    }
}

