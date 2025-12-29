-- Apply schema changes for PlayerRanking migration
-- Step 1: Add new columns to AspNetUsers

-- Check if BirthYearValue column exists before adding
-- SQLite doesn't support IF NOT EXISTS for ALTER TABLE, so we'll just run these
-- If the column already exists, the script will fail at that point

-- Add BirthYearValue column
ALTER TABLE AspNetUsers ADD COLUMN BirthYearValue INTEGER;

-- Add Residence column with default value
ALTER TABLE AspNetUsers ADD COLUMN Residence TEXT DEFAULT 'Singapore';

-- Add Photo column
ALTER TABLE AspNetUsers ADD COLUMN Photo TEXT;

-- Step 2: Create PlayerRanking table
CREATE TABLE IF NOT EXISTS PlayerRanking (
    RankingId TEXT PRIMARY KEY NOT NULL,
    PlayerId TEXT NOT NULL,
    RankingDate INTEGER,
    Ranking TEXT NOT NULL,
    Organization TEXT,
    RankingNote TEXT,
    FOREIGN KEY (PlayerId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
);

-- Create indexes
CREATE INDEX IF NOT EXISTS IX_PlayerRanking_PlayerId ON PlayerRanking(PlayerId);
CREATE INDEX IF NOT EXISTS IX_PlayerRanking_RankingDate ON PlayerRanking(RankingDate);

-- Step 3: Record this migration in __EFMigrationsHistory
INSERT OR IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) 
VALUES ('20241229000000_AddPlayerRankingTable', '6.0.5');

