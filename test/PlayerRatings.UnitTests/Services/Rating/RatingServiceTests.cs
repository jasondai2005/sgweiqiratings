using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using PlayerRatings.Services.Rating;
using Xunit;
using PlayerRatings.Models;
using Match = PlayerRatings.Models.Match;

namespace PlayerRatings.UnitTests.Services.Rating
{
    public class RatingServiceTests
    {
        private readonly Mock<IMemoryCache> _cacheMock;
        private readonly ApplicationDbContext _context;
        private readonly RatingService _service;

        public RatingServiceTests()
        {
            _cacheMock = new Mock<IMemoryCache>();
            
            // Setup in-memory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);
            
            // Setup cache mock to always miss (for simplicity)
            object cacheValue = null;
            _cacheMock.Setup(x => x.TryGetValue(It.IsAny<object>(), out cacheValue))
                .Returns(false);
            _cacheMock.Setup(x => x.CreateEntry(It.IsAny<object>()))
                .Returns(Mock.Of<ICacheEntry>());

            _service = new RatingService(_context, _cacheMock.Object);
        }

        [Fact]
        public void CalculateRatingsFromMatches_EmptyMatches_ReturnsEmptyResults()
        {
            // Arrange
            var matches = new List<Match>();
            var options = new RatingOptions
            {
                CutoffDate = DateTimeOffset.UtcNow,
                SwaOnly = false,
                IsSgLeague = false
            };

            // Act
            var (eloStat, activeUsers) = _service.CalculateRatingsFromMatches(matches, options);

            // Assert
            activeUsers.Should().BeEmpty();
            eloStat.Should().NotBeNull();
        }

        [Fact]
        public void CalculateRatingsFromMatches_WithMatches_TracksActiveUsers()
        {
            // Arrange
            var player1 = CreatePlayer("player1", "Player One");
            var player2 = CreatePlayer("player2", "Player Two");
            
            var matches = new List<Match>
            {
                CreateMatchWithPlayers(player1, player2, 1, 0)
            };
            
            var options = new RatingOptions
            {
                CutoffDate = DateTimeOffset.UtcNow,
                SwaOnly = false,
                IsSgLeague = false
            };

            // Act
            var (eloStat, activeUsers) = _service.CalculateRatingsFromMatches(matches, options);

            // Assert
            activeUsers.Should().Contain(player1);
            activeUsers.Should().Contain(player2);
        }

        [Fact]
        public void CalculateRatingsFromMatches_SkipsZeroFactorMatches()
        {
            // Arrange
            var player1 = CreatePlayer("player1", "Player One");
            var player2 = CreatePlayer("player2", "Player Two");
            
            var matches = new List<Match>
            {
                CreateMatchWithPlayers(player1, player2, 1, 0, factor: 0) // Unrated match
            };
            
            var options = new RatingOptions
            {
                CutoffDate = DateTimeOffset.UtcNow,
                SwaOnly = false,
                IsSgLeague = false
            };

            // Act
            var (eloStat, activeUsers) = _service.CalculateRatingsFromMatches(matches, options);

            // Assert
            activeUsers.Should().BeEmpty(); // No active users since match was unrated
        }

        [Fact]
        public void CalculateRatingsFromMatches_FiltersByUserIds_WhenProvided()
        {
            // Arrange
            var player1 = CreatePlayer("player1", "Player One");
            var player2 = CreatePlayer("player2", "Player Two");
            
            var matches = new List<Match>
            {
                CreateMatchWithPlayers(player1, player2, 1, 0)
            };
            
            var options = new RatingOptions
            {
                CutoffDate = DateTimeOffset.UtcNow,
                SwaOnly = false,
                IsSgLeague = false,
                AllowedUserIds = new HashSet<string> { "player1" } // Only include player1
            };

            // Act
            var (eloStat, activeUsers) = _service.CalculateRatingsFromMatches(matches, options);

            // Assert
            activeUsers.Should().HaveCount(1);
            activeUsers.First().Id.Should().Be("player1");
        }

        [Fact]
        public void GetPlayerRatingsAtDate_ReturnsRatingsForSpecifiedPlayers()
        {
            // Arrange
            var player1 = CreatePlayer("player1", "Player One");
            var player2 = CreatePlayer("player2", "Player Two");
            
            var matches = new List<Match>
            {
                CreateMatchWithPlayers(player1, player2, 1, 0)
            };
            
            var options = new RatingOptions
            {
                CutoffDate = DateTimeOffset.UtcNow,
                SwaOnly = false,
                IsSgLeague = false
            };
            var playerIds = new[] { "player1", "player2" };

            // Act
            var result = _service.GetPlayerRatingsAtDate(matches, playerIds, options);

            // Assert
            result.Should().ContainKey("player1");
            result.Should().ContainKey("player2");
            result["player1"].Rating.Should().BeGreaterThan(result["player2"].Rating); // Winner has higher rating
        }

        [Fact]
        public void InvalidateLeagueCache_RemovesCacheEntry()
        {
            // Arrange
            var leagueId = Guid.NewGuid();

            // Act
            _service.InvalidateLeagueCache(leagueId);

            // Assert
            _cacheMock.Verify(x => x.Remove($"League_{leagueId}"), Times.Once);
        }

        #region Helper Methods

        private static ApplicationUser CreatePlayer(string id, string displayName)
        {
            return new ApplicationUser
            {
                Id = id,
                DisplayName = displayName,
                UserName = displayName.ToLower().Replace(" ", "."),
                Email = $"{displayName.ToLower().Replace(" ", ".")}@test.com",
                Rankings = new List<PlayerRanking>()
            };
        }

        private static Match CreateMatchWithPlayers(
            ApplicationUser player1, 
            ApplicationUser player2, 
            int player1Score, 
            int player2Score,
            double factor = 1.0)
        {
            return new Match
            {
                Id = Guid.NewGuid(),
                FirstPlayer = player1,
                FirstPlayerId = player1.Id,
                SecondPlayer = player2,
                SecondPlayerId = player2.Id,
                FirstPlayerScore = player1Score,
                SecondPlayerScore = player2Score,
                Factor = factor,
                Date = DateTimeOffset.UtcNow.AddDays(-1) // Ensure it's within the rating calculation window
            };
        }

        #endregion
    }
}

