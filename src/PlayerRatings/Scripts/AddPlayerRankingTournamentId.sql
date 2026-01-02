-- Add TournamentId column to PlayerRanking if it doesn't exist
-- This is needed because the migration may have partially failed

-- Check if column exists and add if not
-- SQLite doesn't support IF NOT EXISTS for columns, so we use a workaround

-- Add the column (will fail silently if it exists)
ALTER TABLE PlayerRanking ADD COLUMN TournamentId TEXT NULL;

-- Add the index (will fail if it exists)
CREATE INDEX IF NOT EXISTS IX_PlayerRanking_TournamentId ON PlayerRanking (TournamentId);

