using Microsoft.EntityFrameworkCore.Migrations;

namespace PlayerRatings.Migrations
{
    /// <summary>
    /// Migration to remove the legacy Ranking column from AspNetUsers.
    /// This should be run AFTER the RankingMigrationService has migrated all data
    /// to the PlayerRanking table.
    /// </summary>
    public partial class RemoveRankingColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the legacy Ranking column from AspNetUsers
            // This should only be run after RankingMigrationService has completed
            migrationBuilder.DropColumn(
                name: "Ranking",
                table: "AspNetUsers");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-add the Ranking column if rolling back
            migrationBuilder.AddColumn<string>(
                name: "Ranking",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);
        }
    }
}




