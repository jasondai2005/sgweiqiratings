using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayerRatings.Migrations
{
    public partial class AddTournamentExtendedFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FemaleAwardPhoto",
                table: "TournamentPlayer",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FemalePosition",
                table: "TournamentPlayer",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Photo",
                table: "TournamentPlayer",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Team",
                table: "TournamentPlayer",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamPhoto",
                table: "TournamentPlayer",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeamPosition",
                table: "TournamentPlayer",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalLinks",
                table: "Tournament",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Tournament",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Photo",
                table: "Tournament",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StandingsPhoto",
                table: "Tournament",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsFemaleAward",
                table: "Tournament",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsPersonalAward",
                table: "Tournament",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsTeamAward",
                table: "Tournament",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GameRecord",
                table: "Match",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MatchPhoto",
                table: "Match",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MatchResultPhoto",
                table: "Match",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FemaleAwardPhoto",
                table: "TournamentPlayer");

            migrationBuilder.DropColumn(
                name: "FemalePosition",
                table: "TournamentPlayer");

            migrationBuilder.DropColumn(
                name: "Photo",
                table: "TournamentPlayer");

            migrationBuilder.DropColumn(
                name: "Team",
                table: "TournamentPlayer");

            migrationBuilder.DropColumn(
                name: "TeamPhoto",
                table: "TournamentPlayer");

            migrationBuilder.DropColumn(
                name: "TeamPosition",
                table: "TournamentPlayer");

            migrationBuilder.DropColumn(
                name: "ExternalLinks",
                table: "Tournament");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Tournament");

            migrationBuilder.DropColumn(
                name: "Photo",
                table: "Tournament");

            migrationBuilder.DropColumn(
                name: "StandingsPhoto",
                table: "Tournament");

            migrationBuilder.DropColumn(
                name: "SupportsFemaleAward",
                table: "Tournament");

            migrationBuilder.DropColumn(
                name: "SupportsPersonalAward",
                table: "Tournament");

            migrationBuilder.DropColumn(
                name: "SupportsTeamAward",
                table: "Tournament");

            migrationBuilder.DropColumn(
                name: "GameRecord",
                table: "Match");

            migrationBuilder.DropColumn(
                name: "MatchPhoto",
                table: "Match");

            migrationBuilder.DropColumn(
                name: "MatchResultPhoto",
                table: "Match");
        }
    }
}
