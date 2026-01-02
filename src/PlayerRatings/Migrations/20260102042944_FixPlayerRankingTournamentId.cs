using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayerRatings.Migrations
{
    public partial class FixPlayerRankingTournamentId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add TournamentId column to PlayerRanking (may have been missed in previous migration)
            migrationBuilder.AddColumn<Guid>(
                name: "TournamentId",
                table: "PlayerRanking",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerRanking_TournamentId",
                table: "PlayerRanking",
                column: "TournamentId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerRanking_Tournament_TournamentId",
                table: "PlayerRanking",
                column: "TournamentId",
                principalTable: "Tournament",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerRanking_Tournament_TournamentId",
                table: "PlayerRanking");

            migrationBuilder.DropIndex(
                name: "IX_PlayerRanking_TournamentId",
                table: "PlayerRanking");

            migrationBuilder.DropColumn(
                name: "TournamentId",
                table: "PlayerRanking");
        }
    }
}
