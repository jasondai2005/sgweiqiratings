using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayerRatings.Migrations
{
    public partial class AddPerformanceIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Match table indexes
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Match_LeagueId\" ON \"Match\" (\"LeagueId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Match_Date\" ON \"Match\" (\"Date\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Match_FirstPlayerId\" ON \"Match\" (\"FirstPlayerId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Match_SecondPlayerId\" ON \"Match\" (\"SecondPlayerId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Match_LeagueId_Date\" ON \"Match\" (\"LeagueId\", \"Date\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Match_TournamentId\" ON \"Match\" (\"TournamentId\");");

            // Tournament table indexes
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Tournament_LeagueId\" ON \"Tournament\" (\"LeagueId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Tournament_LeagueId_StartDate\" ON \"Tournament\" (\"LeagueId\", \"StartDate\");");

            // TournamentPlayer table indexes
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_TournamentPlayer_PlayerId\" ON \"TournamentPlayer\" (\"PlayerId\");");

            // PlayerRanking table indexes
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_PlayerRanking_PlayerId\" ON \"PlayerRanking\" (\"PlayerId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_PlayerRanking_TournamentId\" ON \"PlayerRanking\" (\"TournamentId\");");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Match_LeagueId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Match_Date\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Match_FirstPlayerId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Match_SecondPlayerId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Match_LeagueId_Date\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Match_TournamentId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Tournament_LeagueId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Tournament_LeagueId_StartDate\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_TournamentPlayer_PlayerId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_PlayerRanking_PlayerId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_PlayerRanking_TournamentId\";");
        }
    }
}
