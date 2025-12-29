using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PlayerRatings.Migrations
{
    public partial class AddPlayerRankingTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new columns to AspNetUsers
            migrationBuilder.AddColumn<int>(
                name: "BirthYearValue",
                table: "AspNetUsers",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Residence",
                table: "AspNetUsers",
                maxLength: 100,
                nullable: true,
                defaultValue: "Singapore");

            migrationBuilder.AddColumn<string>(
                name: "Photo",
                table: "AspNetUsers",
                maxLength: 500,
                nullable: true);

            // Create PlayerRanking table
            migrationBuilder.CreateTable(
                name: "PlayerRanking",
                columns: table => new
                {
                    RankingId = table.Column<Guid>(nullable: false),
                    PlayerId = table.Column<string>(nullable: false),
                    RankingDate = table.Column<long>(nullable: true), // DateTimeOffset stored as binary for SQLite
                    Ranking = table.Column<string>(maxLength: 10, nullable: false),
                    Organization = table.Column<string>(maxLength: 50, nullable: true),
                    RankingNote = table.Column<string>(maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerRanking", x => x.RankingId);
                    table.ForeignKey(
                        name: "FK_PlayerRanking_AspNetUsers_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create index on PlayerId for faster lookups
            migrationBuilder.CreateIndex(
                name: "IX_PlayerRanking_PlayerId",
                table: "PlayerRanking",
                column: "PlayerId");

            // Create index on RankingDate for ordering
            migrationBuilder.CreateIndex(
                name: "IX_PlayerRanking_RankingDate",
                table: "PlayerRanking",
                column: "RankingDate");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PlayerRanking");

            migrationBuilder.DropColumn(name: "BirthYearValue", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "Residence", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "Photo", table: "AspNetUsers");
        }
    }
}



