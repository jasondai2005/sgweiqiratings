using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PlayerRatings.Models
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Performance indexes for Match table
            builder.Entity<Match>(entity =>
            {
                entity.HasIndex(m => m.LeagueId).HasDatabaseName("IX_Match_LeagueId");
                entity.HasIndex(m => m.Date).HasDatabaseName("IX_Match_Date");
                entity.HasIndex(m => m.FirstPlayerId).HasDatabaseName("IX_Match_FirstPlayerId");
                entity.HasIndex(m => m.SecondPlayerId).HasDatabaseName("IX_Match_SecondPlayerId");
                // Composite index for common query pattern: filter by league, order by date
                entity.HasIndex(m => new { m.LeagueId, m.Date }).HasDatabaseName("IX_Match_LeagueId_Date");
            });

            // Performance indexes for LeaguePlayer table
            builder.Entity<LeaguePlayer>(entity =>
            {
                entity.HasIndex(lp => lp.LeagueId).HasDatabaseName("IX_LeaguePlayer_LeagueId");
                entity.HasIndex(lp => lp.UserId).HasDatabaseName("IX_LeaguePlayer_UserId");
                // Composite index for common query: find user's league membership
                entity.HasIndex(lp => new { lp.LeagueId, lp.UserId }).HasDatabaseName("IX_LeaguePlayer_LeagueId_UserId");
            });

            // Performance index for Invite table
            builder.Entity<Invite>(entity =>
            {
                entity.HasIndex(i => i.InvitedById).HasDatabaseName("IX_Invite_InvitedById");
            });

            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                // SQLite does not have proper support for DateTimeOffset via Entity Framework Core, see the limitations
                // here: https://docs.microsoft.com/en-us/ef/core/providers/sqlite/limitations#query-limitations
                // To work around this, when the Sqlite database provider is used, all model properties of type DateTimeOffset
                // use the DateTimeOffsetToBinaryConverter
                // Based on: https://github.com/aspnet/EntityFrameworkCore/issues/10784#issuecomment-415769754
                // This only supports millisecond precision, but should be sufficient for most use cases.
                foreach (var entityType in builder.Model.GetEntityTypes())
                {
                    var properties = entityType.ClrType.GetProperties().Where(p => p.PropertyType == typeof(DateTimeOffset)
                                                                                || p.PropertyType == typeof(DateTimeOffset?));
                    foreach (var property in properties)
                    {
                        builder
                            .Entity(entityType.Name)
                            .Property(property.Name)
                            .HasConversion(new DateTimeOffsetToBinaryConverter());
                    }
                }
            }
        }

        public DbSet<Match> Match { get; set; }

        public DbSet<League> League { get; set; }

        public DbSet<LeaguePlayer> LeaguePlayers { get; set; }

        public DbSet<Invite> Invites { get; set; }

        public DbSet<PlayerRanking> PlayerRankings { get; set; }
    }
}
