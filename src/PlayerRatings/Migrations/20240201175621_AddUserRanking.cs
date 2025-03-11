using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PlayerRatings.Migrations
{
    public partial class AddUserRanking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Ranking",
                table: "AspNetUsers",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Ranking", table: "AspNetUsers");
        }
    }
}
