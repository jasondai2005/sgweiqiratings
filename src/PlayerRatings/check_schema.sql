CREATE TABLE "AspNetRoles" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetRoles" PRIMARY KEY,
    "Name" TEXT NULL,
    "NormalizedName" TEXT NULL,
    "ConcurrencyStamp" TEXT NULL
);


CREATE TABLE "AspNetUsers" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetUsers" PRIMARY KEY,
    "DisplayName" TEXT NULL,
    "BirthYearValue" INTEGER NULL,
    "Residence" TEXT NULL,
    "Photo" TEXT NULL,
    "UserName" TEXT NULL,
    "NormalizedUserName" TEXT NULL,
    "Email" TEXT NULL,
    "NormalizedEmail" TEXT NULL,
    "EmailConfirmed" INTEGER NOT NULL,
    "PasswordHash" TEXT NULL,
    "SecurityStamp" TEXT NULL,
    "ConcurrencyStamp" TEXT NULL,
    "PhoneNumber" TEXT NULL,
    "PhoneNumberConfirmed" INTEGER NOT NULL,
    "TwoFactorEnabled" INTEGER NOT NULL,
    "LockoutEnd" INTEGER NULL,
    "LockoutEnabled" INTEGER NOT NULL,
    "AccessFailedCount" INTEGER NOT NULL
);


CREATE TABLE "AspNetRoleClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY AUTOINCREMENT,
    "RoleId" TEXT NOT NULL,
    "ClaimType" TEXT NULL,
    "ClaimValue" TEXT NULL,
    CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AspNetUserClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY AUTOINCREMENT,
    "UserId" TEXT NOT NULL,
    "ClaimType" TEXT NULL,
    "ClaimValue" TEXT NULL,
    CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AspNetUserLogins" (
    "LoginProvider" TEXT NOT NULL,
    "ProviderKey" TEXT NOT NULL,
    "ProviderDisplayName" TEXT NULL,
    "UserId" TEXT NOT NULL,
    CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey"),
    CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AspNetUserRoles" (
    "UserId" TEXT NOT NULL,
    "RoleId" TEXT NOT NULL,
    CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId"),
    CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AspNetUserTokens" (
    "UserId" TEXT NOT NULL,
    "LoginProvider" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Value" TEXT NULL,
    CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name"),
    CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Invite" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Invite" PRIMARY KEY,
    "CreatedOn" INTEGER NOT NULL,
    "InvitedById" TEXT NULL,
    "CreatedUserId" TEXT NULL,
    CONSTRAINT "FK_Invite_AspNetUsers_CreatedUserId" FOREIGN KEY ("CreatedUserId") REFERENCES "AspNetUsers" ("Id"),
    CONSTRAINT "FK_Invite_AspNetUsers_InvitedById" FOREIGN KEY ("InvitedById") REFERENCES "AspNetUsers" ("Id")
);


CREATE TABLE "League" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_League" PRIMARY KEY,
    "Name" TEXT NULL,
    "CreatedByUserId" TEXT NULL,
    CONSTRAINT "FK_League_AspNetUsers_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "AspNetUsers" ("Id")
);


CREATE TABLE "LeaguePlayer" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_LeaguePlayer" PRIMARY KEY,
    "IsBlocked" INTEGER NOT NULL,
    "UserId" TEXT NULL,
    "LeagueId" TEXT NOT NULL,
    CONSTRAINT "FK_LeaguePlayer_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id"),
    CONSTRAINT "FK_LeaguePlayer_League_LeagueId" FOREIGN KEY ("LeagueId") REFERENCES "League" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Tournament" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Tournament" PRIMARY KEY,
    "LeagueId" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Ordinal" TEXT NULL,
    "Group" TEXT NULL,
    "Organizer" TEXT NULL,
    "Location" TEXT NULL,
    "StartDate" INTEGER NULL,
    "EndDate" INTEGER NULL,
    "TournamentType" TEXT NULL,
    "Factor" REAL NULL,
    CONSTRAINT "FK_Tournament_League_LeagueId" FOREIGN KEY ("LeagueId") REFERENCES "League" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Match" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Match" PRIMARY KEY,
    "Date" INTEGER NOT NULL,
    "FirstPlayerScore" INTEGER NOT NULL,
    "SecondPlayerScore" INTEGER NOT NULL,
    "Factor" REAL NULL,
    "LeagueId" TEXT NOT NULL,
    "CreatedByUserId" TEXT NULL,
    "FirstPlayerId" TEXT NULL,
    "SecondPlayerId" TEXT NULL,
    "MatchName" TEXT NULL,
    "TournamentId" TEXT NULL,
    "Round" INTEGER NULL,
    CONSTRAINT "FK_Match_AspNetUsers_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "AspNetUsers" ("Id"),
    CONSTRAINT "FK_Match_AspNetUsers_FirstPlayerId" FOREIGN KEY ("FirstPlayerId") REFERENCES "AspNetUsers" ("Id"),
    CONSTRAINT "FK_Match_AspNetUsers_SecondPlayerId" FOREIGN KEY ("SecondPlayerId") REFERENCES "AspNetUsers" ("Id"),
    CONSTRAINT "FK_Match_League_LeagueId" FOREIGN KEY ("LeagueId") REFERENCES "League" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Match_Tournament_TournamentId" FOREIGN KEY ("TournamentId") REFERENCES "Tournament" ("Id")
);


CREATE TABLE "PlayerRanking" (
    "RankingId" TEXT NOT NULL CONSTRAINT "PK_PlayerRanking" PRIMARY KEY,
    "PlayerId" TEXT NOT NULL,
    "RankingDate" INTEGER NULL,
    "Ranking" TEXT NOT NULL,
    "Organization" TEXT NULL,
    "RankingNote" TEXT NULL,
    "TournamentId" TEXT NULL,
    CONSTRAINT "FK_PlayerRanking_AspNetUsers_PlayerId" FOREIGN KEY ("PlayerId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_PlayerRanking_Tournament_TournamentId" FOREIGN KEY ("TournamentId") REFERENCES "Tournament" ("Id")
);


CREATE TABLE "TournamentPlayer" (
    "TournamentId" TEXT NOT NULL,
    "PlayerId" TEXT NOT NULL,
    "Position" INTEGER NULL,
    "PromotionId" TEXT NULL,
    CONSTRAINT "PK_TournamentPlayer" PRIMARY KEY ("TournamentId", "PlayerId"),
    CONSTRAINT "FK_TournamentPlayer_AspNetUsers_PlayerId" FOREIGN KEY ("PlayerId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_TournamentPlayer_PlayerRanking_PromotionId" FOREIGN KEY ("PromotionId") REFERENCES "PlayerRanking" ("RankingId"),
    CONSTRAINT "FK_TournamentPlayer_Tournament_TournamentId" FOREIGN KEY ("TournamentId") REFERENCES "Tournament" ("Id") ON DELETE CASCADE
);


CREATE INDEX "IX_AspNetRoleClaims_RoleId" ON "AspNetRoleClaims" ("RoleId");


CREATE UNIQUE INDEX "RoleNameIndex" ON "AspNetRoles" ("NormalizedName");


CREATE INDEX "IX_AspNetUserClaims_UserId" ON "AspNetUserClaims" ("UserId");


CREATE INDEX "IX_AspNetUserLogins_UserId" ON "AspNetUserLogins" ("UserId");


CREATE INDEX "IX_AspNetUserRoles_RoleId" ON "AspNetUserRoles" ("RoleId");


CREATE INDEX "EmailIndex" ON "AspNetUsers" ("NormalizedEmail");


CREATE UNIQUE INDEX "UserNameIndex" ON "AspNetUsers" ("NormalizedUserName");


CREATE INDEX "IX_Invite_CreatedUserId" ON "Invite" ("CreatedUserId");


CREATE INDEX "IX_Invite_InvitedById" ON "Invite" ("InvitedById");


CREATE INDEX "IX_League_CreatedByUserId" ON "League" ("CreatedByUserId");


CREATE INDEX "IX_LeaguePlayer_LeagueId" ON "LeaguePlayer" ("LeagueId");


CREATE INDEX "IX_LeaguePlayer_LeagueId_UserId" ON "LeaguePlayer" ("LeagueId", "UserId");


CREATE INDEX "IX_LeaguePlayer_UserId" ON "LeaguePlayer" ("UserId");


CREATE INDEX "IX_Match_CreatedByUserId" ON "Match" ("CreatedByUserId");


CREATE INDEX "IX_Match_Date" ON "Match" ("Date");


CREATE INDEX "IX_Match_FirstPlayerId" ON "Match" ("FirstPlayerId");


CREATE INDEX "IX_Match_LeagueId" ON "Match" ("LeagueId");


CREATE INDEX "IX_Match_LeagueId_Date" ON "Match" ("LeagueId", "Date");


CREATE INDEX "IX_Match_SecondPlayerId" ON "Match" ("SecondPlayerId");


CREATE INDEX "IX_Match_TournamentId" ON "Match" ("TournamentId");


CREATE INDEX "IX_PlayerRanking_PlayerId" ON "PlayerRanking" ("PlayerId");


CREATE INDEX "IX_PlayerRanking_TournamentId" ON "PlayerRanking" ("TournamentId");


CREATE INDEX "IX_Tournament_LeagueId" ON "Tournament" ("LeagueId");


CREATE INDEX "IX_Tournament_LeagueId_StartDate" ON "Tournament" ("LeagueId", "StartDate");


CREATE INDEX "IX_TournamentPlayer_PlayerId" ON "TournamentPlayer" ("PlayerId");


CREATE INDEX "IX_TournamentPlayer_PromotionId" ON "TournamentPlayer" ("PromotionId");


