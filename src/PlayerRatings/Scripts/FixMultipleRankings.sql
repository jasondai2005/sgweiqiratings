-- SQLite Script: Fix players with multiple rankings (e.g., "1D (2D)")
-- This handles cases where a player has both SWA and TGA rankings

-- ============================================
-- STEP 1: Identify players with combined rankings
-- ============================================

-- Preview players who might have combined rankings (SWA + TGA format)
-- Pattern: "XD (YD)" or "XK (YK)" where X is SWA grade and Y is TGA grade
SELECT 
    u.Id,
    u.DisplayName,
    pr.Ranking AS CurrentRanking,
    pr.Organization AS CurrentOrg,
    pr.RankingDate,
    pr.RankingId
FROM AspNetUsers u
JOIN PlayerRanking pr ON u.Id = pr.PlayerId
WHERE pr.Ranking LIKE '% (%)'
   OR pr.Ranking LIKE '%D (%D)'
   OR pr.Ranking LIKE '%K (%K)'
   OR pr.Ranking LIKE '%)';

-- ============================================
-- STEP 2: Check for players missing TGA rankings
-- ============================================

-- Find players who have SWA ranking but the ranking string contains TGA notation
SELECT 
    u.Id,
    u.DisplayName,
    pr.Ranking,
    pr.Organization,
    -- Extract SWA part (before the parenthesis)
    TRIM(SUBSTR(pr.Ranking, 1, INSTR(pr.Ranking, '(') - 1)) AS SwaRanking,
    -- Extract TGA part (inside parenthesis)
    REPLACE(REPLACE(SUBSTR(pr.Ranking, INSTR(pr.Ranking, '(')), '(', ''), ')', '') AS TgaRanking
FROM AspNetUsers u
JOIN PlayerRanking pr ON u.Id = pr.PlayerId
WHERE pr.Ranking LIKE '% (%)'
  AND pr.Organization = 'SWA';

-- ============================================
-- STEP 3: Fix the data
-- ============================================

-- First, update existing records to have only the SWA ranking
-- (UNCOMMENT TO EXECUTE)
/*
UPDATE PlayerRanking
SET Ranking = TRIM(SUBSTR(Ranking, 1, INSTR(Ranking, '(') - 1))
WHERE Ranking LIKE '% (%)'
  AND Organization = 'SWA';
*/

-- Then, insert new TGA ranking records for these players
-- (UNCOMMENT TO EXECUTE)
/*
INSERT INTO PlayerRanking (RankingId, PlayerId, RankingDate, Ranking, Organization, RankingNote)
SELECT 
    lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)),2) || '-' || substr('89ab',abs(random()) % 4 + 1, 1) || substr(hex(randomblob(2)),2) || '-' || hex(randomblob(6))) AS RankingId,
    pr.PlayerId,
    pr.RankingDate,
    REPLACE(REPLACE(SUBSTR(pr.Ranking, INSTR(pr.Ranking, '(')), '(', ''), ')', '') AS Ranking,
    'TGA' AS Organization,
    'Migrated from combined ranking' AS RankingNote
FROM PlayerRanking pr
WHERE pr.Ranking LIKE '% (%)'
  AND pr.Organization = 'SWA';
*/

-- ============================================
-- STEP 4: Verification
-- ============================================

-- Check for any remaining combined rankings
SELECT 'Remaining combined rankings:' AS Info, COUNT(*)
FROM PlayerRanking
WHERE Ranking LIKE '% (%)'
   OR Ranking LIKE '%)';

-- List all TGA rankings
SELECT 'TGA Rankings:' AS Info, COUNT(*)
FROM PlayerRanking
WHERE Organization = 'TGA';



