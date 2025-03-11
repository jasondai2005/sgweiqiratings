using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PlayerRatings.Migrations
{
    public partial class Customization : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MatchName",
                table: "Match",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "MatchName", table: "Match");
        }
    }
}
