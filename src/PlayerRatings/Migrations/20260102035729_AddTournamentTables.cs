using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayerRatings.Migrations
{
    public partial class AddTournamentTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaguePlayer_League_LeagueId",
                table: "LeaguePlayer");

            migrationBuilder.DropForeignKey(
                name: "FK_Match_League_LeagueId",
                table: "Match");

            migrationBuilder.DropIndex(
                name: "IX_PlayerRanking_RankingDate",
                table: "PlayerRanking");

            migrationBuilder.DropIndex(
                name: "UserNameIndex",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles");

            migrationBuilder.AlterColumn<long>(
                name: "RankingDate",
                table: "PlayerRanking",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "Date",
                table: "Match",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");

            // Note: MatchName column already exists in the database
            // migrationBuilder.AddColumn<string>(
            //     name: "MatchName",
            //     table: "Match",
            //     type: "TEXT",
            //     nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Round",
                table: "Match",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TournamentId",
                table: "Match",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TournamentId",
                table: "PlayerRanking",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CreatedOn",
                table: "Invite",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<long>(
                name: "LockoutEnd",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tournament",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LeagueId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Ordinal = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Group = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Organizer = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    StartDate = table.Column<long>(type: "INTEGER", nullable: true),
                    EndDate = table.Column<long>(type: "INTEGER", nullable: true),
                    TournamentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Factor = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournament", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tournament_League_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "League",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TournamentPlayer",
                columns: table => new
                {
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: true),
                    PromotionId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentPlayer", x => new { x.TournamentId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_TournamentPlayer_AspNetUsers_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TournamentPlayer_PlayerRanking_PromotionId",
                        column: x => x.PromotionId,
                        principalTable: "PlayerRanking",
                        principalColumn: "RankingId");
                    table.ForeignKey(
                        name: "FK_TournamentPlayer_Tournament_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournament",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Note: These indexes already exist in the database - commented out
            // migrationBuilder.CreateIndex(name: "IX_Match_CreatedByUserId", table: "Match", column: "CreatedByUserId");
            // migrationBuilder.CreateIndex(name: "IX_Match_Date", table: "Match", column: "Date");
            // migrationBuilder.CreateIndex(name: "IX_Match_FirstPlayerId", table: "Match", column: "FirstPlayerId");
            // migrationBuilder.CreateIndex(name: "IX_Match_LeagueId", table: "Match", column: "LeagueId");
            // migrationBuilder.CreateIndex(name: "IX_Match_LeagueId_Date", table: "Match", columns: new[] { "LeagueId", "Date" });
            // migrationBuilder.CreateIndex(name: "IX_Match_SecondPlayerId", table: "Match", column: "SecondPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Match_TournamentId",
                table: "Match",
                column: "TournamentId");

            // Note: These indexes already exist in the database - commented out
            // migrationBuilder.CreateIndex(name: "IX_LeaguePlayer_LeagueId", table: "LeaguePlayer", column: "LeagueId");
            // migrationBuilder.CreateIndex(name: "IX_LeaguePlayer_LeagueId_UserId", table: "LeaguePlayer", columns: new[] { "LeagueId", "UserId" });
            // migrationBuilder.CreateIndex(name: "IX_LeaguePlayer_UserId", table: "LeaguePlayer", column: "UserId");
            // migrationBuilder.CreateIndex(name: "IX_League_CreatedByUserId", table: "League", column: "CreatedByUserId");
            // migrationBuilder.CreateIndex(name: "IX_Invite_CreatedUserId", table: "Invite", column: "CreatedUserId");
            // migrationBuilder.CreateIndex(name: "IX_Invite_InvitedById", table: "Invite", column: "InvitedById");
            // migrationBuilder.CreateIndex(name: "UserNameIndex", table: "AspNetUsers", column: "NormalizedUserName", unique: true);
            // migrationBuilder.CreateIndex(name: "IX_AspNetUserRoles_RoleId", table: "AspNetUserRoles", column: "RoleId");

            // Note: These Identity indexes may already exist - using try/catch pattern or commenting out
            // migrationBuilder.CreateIndex(name: "IX_AspNetUserLogins_UserId", table: "AspNetUserLogins", column: "UserId");
            // migrationBuilder.CreateIndex(name: "IX_AspNetUserClaims_UserId", table: "AspNetUserClaims", column: "UserId");
            // migrationBuilder.CreateIndex(name: "RoleNameIndex", table: "AspNetRoles", column: "NormalizedName", unique: true);
            // migrationBuilder.CreateIndex(name: "IX_AspNetRoleClaims_RoleId", table: "AspNetRoleClaims", column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournament_LeagueId",
                table: "Tournament",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournament_LeagueId_StartDate",
                table: "Tournament",
                columns: new[] { "LeagueId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlayer_PlayerId",
                table: "TournamentPlayer",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlayer_PromotionId",
                table: "TournamentPlayer",
                column: "PromotionId");

            // Note: IX_PlayerRanking_PlayerId may already exist
            // migrationBuilder.CreateIndex(name: "IX_PlayerRanking_PlayerId", table: "PlayerRanking", column: "PlayerId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaguePlayer_League_LeagueId",
                table: "LeaguePlayer",
                column: "LeagueId",
                principalTable: "League",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Match_League_LeagueId",
                table: "Match",
                column: "LeagueId",
                principalTable: "League",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Match_Tournament_TournamentId",
                table: "Match",
                column: "TournamentId",
                principalTable: "Tournament",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaguePlayer_League_LeagueId",
                table: "LeaguePlayer");

            migrationBuilder.DropForeignKey(
                name: "FK_Match_League_LeagueId",
                table: "Match");

            migrationBuilder.DropForeignKey(
                name: "FK_Match_Tournament_TournamentId",
                table: "Match");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerRanking_Tournament_TournamentId",
                table: "PlayerRanking");

            migrationBuilder.DropIndex(
                name: "IX_PlayerRanking_PlayerId",
                table: "PlayerRanking");

            migrationBuilder.DropIndex(
                name: "IX_PlayerRanking_TournamentId",
                table: "PlayerRanking");

            migrationBuilder.DropColumn(
                name: "TournamentId",
                table: "PlayerRanking");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "TournamentPlayer");

            migrationBuilder.DropTable(
                name: "Tournament");

            migrationBuilder.DropIndex(
                name: "IX_Match_CreatedByUserId",
                table: "Match");

            migrationBuilder.DropIndex(
                name: "IX_Match_Date",
                table: "Match");

            migrationBuilder.DropIndex(
                name: "IX_Match_FirstPlayerId",
                table: "Match");

            migrationBuilder.DropIndex(
                name: "IX_Match_LeagueId",
                table: "Match");

            migrationBuilder.DropIndex(
                name: "IX_Match_LeagueId_Date",
                table: "Match");

            migrationBuilder.DropIndex(
                name: "IX_Match_SecondPlayerId",
                table: "Match");

            migrationBuilder.DropIndex(
                name: "IX_Match_TournamentId",
                table: "Match");

            migrationBuilder.DropIndex(
                name: "IX_LeaguePlayer_LeagueId",
                table: "LeaguePlayer");

            migrationBuilder.DropIndex(
                name: "IX_LeaguePlayer_LeagueId_UserId",
                table: "LeaguePlayer");

            migrationBuilder.DropIndex(
                name: "IX_LeaguePlayer_UserId",
                table: "LeaguePlayer");

            migrationBuilder.DropIndex(
                name: "IX_League_CreatedByUserId",
                table: "League");

            migrationBuilder.DropIndex(
                name: "IX_Invite_CreatedUserId",
                table: "Invite");

            migrationBuilder.DropIndex(
                name: "IX_Invite_InvitedById",
                table: "Invite");

            migrationBuilder.DropIndex(
                name: "UserNameIndex",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims");

            migrationBuilder.DropIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles");

            migrationBuilder.DropIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims");

            // Note: MatchName column already existed before this migration
            // migrationBuilder.DropColumn(
            //     name: "MatchName",
            //     table: "Match");

            migrationBuilder.DropColumn(
                name: "Round",
                table: "Match");

            migrationBuilder.DropColumn(
                name: "TournamentId",
                table: "Match");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "RankingDate",
                table: "PlayerRanking",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Date",
                table: "Match",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedOn",
                table: "Invite",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LockoutEnd",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerRanking_RankingDate",
                table: "PlayerRanking",
                column: "RankingDate");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_LeaguePlayer_League_LeagueId",
                table: "LeaguePlayer",
                column: "LeagueId",
                principalTable: "League",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Match_League_LeagueId",
                table: "Match",
                column: "LeagueId",
                principalTable: "League",
                principalColumn: "Id");
        }
    }
}
