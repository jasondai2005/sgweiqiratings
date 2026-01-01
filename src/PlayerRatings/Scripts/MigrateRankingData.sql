-- SQLite Data Migration Script: Move ranking data from Ranking column to PlayerRanking table
-- This script should be run AFTER the EF migration creates the new tables/columns

-- ============================================
-- STEP 1: Extract Birth Year to new column
-- ============================================

-- Preview birth years to be extracted
SELECT 'Birth years to extract:' AS Info, 
       Id, 
       DisplayName,
       Ranking,
       CASE 
           WHEN Ranking LIKE '%BY:%' THEN 
               SUBSTR(Ranking, INSTR(Ranking, 'BY:') + 3, 4)
           ELSE NULL 
       END AS ExtractedBirthYear
FROM AspNetUsers
WHERE Ranking LIKE '%BY:%';

-- -- Update BirthYearValue column (UNCOMMENT TO EXECUTE)
-- UPDATE AspNetUsers
-- SET BirthYearValue = CAST(SUBSTR(Ranking, INSTR(Ranking, 'BY:') + 3, 4) AS INTEGER)
-- WHERE Ranking LIKE '%BY:%';

-- ============================================
-- STEP 2: Parse ranking history entries
-- ============================================

-- Note: Due to SQLite limitations with complex string parsing,
-- rankings with multiple entries (e.g., "1D:01/06/2024;1K:01/03/2024")
-- should ideally be migrated using the C# migration utility.
-- 
-- This script handles simple cases:

-- Preview players with simple rankings (no dates, no semicolons)
SELECT 'Simple rankings (no dates):' AS Info,
       Id,
       DisplayName,
       Ranking,
       CASE 
           -- TGA ranking: (1D) format
           WHEN Ranking LIKE '(%D)' OR Ranking LIKE '(%K)' OR Ranking LIKE '(%P)' THEN 
               REPLACE(REPLACE(Ranking, '(', ''), ')', '')
           -- Foreign ranking: [1D CWA] format  
           WHEN Ranking LIKE '[%]' THEN
               SUBSTR(Ranking, 2, INSTR(Ranking, ' ') - 2)
           -- SWA ranking: 1D format
           ELSE 
               CASE WHEN Ranking LIKE '%:%' THEN SUBSTR(Ranking, 1, INSTR(Ranking, ':') - 1) ELSE Ranking END
       END AS ExtractedRanking,
       CASE 
           WHEN Ranking LIKE '(%D)' OR Ranking LIKE '(%K)' OR Ranking LIKE '(%P)' THEN 'TGA'
           WHEN Ranking LIKE '[%]' THEN 
               TRIM(SUBSTR(Ranking, INSTR(Ranking, ' ') + 1, INSTR(Ranking, ']') - INSTR(Ranking, ' ') - 1))
           ELSE 'SWA'
       END AS Organization
FROM AspNetUsers
WHERE Ranking IS NOT NULL 
  AND Ranking != ''
  AND Ranking NOT LIKE '%;%'  -- Exclude complex multi-ranking entries
LIMIT 20;

-- ============================================
-- RECOMMENDED: Use C# Migration Utility
-- ============================================
-- 
-- For complete and accurate migration, run the C# migration utility:
-- dotnet run --project PlayerRatings -- --migrate-rankings
--
-- The C# utility properly handles:
-- - Multiple ranking entries (1D:01/06/2024;1K:01/03/2024)
-- - Date parsing in various formats
-- - Complex foreign ranking formats [1D China 6D]
-- - Birth year extraction
-- - Error handling and logging

-- ============================================
-- VERIFICATION QUERIES
-- ============================================

-- Count of players with rankings to migrate
SELECT 'Players with rankings:' AS Info, COUNT(*) 
FROM AspNetUsers 
WHERE Ranking IS NOT NULL AND Ranking != '';

-- Count of rankings after migration
-- SELECT 'Migrated rankings:' AS Info, COUNT(*) FROM PlayerRanking;

-- Players whose rankings were migrated
-- SELECT 'Players in PlayerRanking:' AS Info, COUNT(DISTINCT PlayerId) FROM PlayerRanking;




