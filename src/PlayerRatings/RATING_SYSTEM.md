# Singapore Weiqi (Go) Rating System

## Overview

We use an Elo-based rating system for Singapore Weiqi (Go) Competitions to track player strength. The system is designed to provide accurate ratings while accommodating players from various backgrounds and ranking systems.

---

## The Elo Rating System

### How It Works

The Elo system calculates a player's new rating after each game based on:
1. **Current ratings** of both players
2. **Expected score** (probability of winning based on rating difference)
3. **Actual result** (win = 1.0, draw = 0.5, loss = 0.0)
4. **K-factor** (volatility factor that determines how much ratings change)

### Rating Update Formula

```
New Rating = Old Rating + K × (Actual Score - Expected Score)
```

Where the **Expected Score** is calculated using the following formula:

```
Expected Score = 1 / (1 + 10^((Opponent Rating - Player Rating) / 400))
```

For example:
- Equal ratings → Expected score = 0.5 (50% win probability)
- 200 points higher → Expected score ≈ 0.76 (76% win probability)
- 400 points higher → Expected score ≈ 0.91 (91% win probability)

### Minimum Rating

Ratings cannot fall below **900 points**.

---

## Rating Scale

### Professionals

| Rank | Rating |
|------|--------|
| 1P   | 2740   |
| 2P   | 2780   |
| 3P   | 2820   |
| ...  | +40 per level |
| 9P   | 3060   |

### Dan Grades

| Rank | Rating |
|------|--------|
| 7D   | 2700   |
| 6D   | 2600   |
| 5D   | 2500   |
| 4D   | 2400   |
| 3D   | 2300   |
| 2D   | 2200   |
| 1D   | 2100   |

**Note:** Foreign dan rankings are valued 100 points lower than SWA rankings (one level down), except foreign 1D which is set at 2075 (between SWA 1K and 1D).

### Kyu Grades

| Rank Range | Points Per Level | Example Ratings |
|------------|------------------|-----------------|
| 1K         | 50 below 1D      | 2050            |
| 2K-5K      | 25 per level     | 2K=2025, 5K=1950 |
| 6K-10K     | 30 per level     | 6K=1920, 10K=1800 |
| 11K-20K    | 40 per level     | 11K=1760, 20K=1400 |
| 21K-30K    | 50 per level     | 21K=1350, 30K=900 |

---

## K-Factor (Volatility)

The K-factor determines how much a single game affects a player's rating. Higher K-factors mean faster rating changes.

### K-Factor Tiers

| Rating Range | Rank Range | K-Factor |
|--------------|------------|----------|
| ≥ 2720       | Pro        | 6        |
| 2300-2719    | 3D+        | 12       |
| 1950-2299    | 5K-2D      | 20       |
| 1600-1949    | 15K-6K     | 28       |
| < 1600       | 16K+       | 36       |

### Reference: EGF Formula

For comparison, the European Go Federation (EGF) uses a continuous formula:

```
K = ((3300 - Rating) / 200)^1.6
```

| Rank | Rating | EGF K |
|------|--------|-------|
| 5K   | 1950   | 21.2  |
| 1K   | 2050   | 18.8  |
| 1D   | 2100   | 17.6  |
| 2D   | 2200   | 15.3  |
| 3D   | 2300   | 13.1  |
| 5D   | 2500   | 9.2   |
| 7D   | 2700   | 5.8   |
| 1P   | 2740   | 5.2   |

---

## Initial Rating Assignment

### Players with Recognized Rankings

A player's initial rating is determined by their ranking from an organization with comparable ranking standards:

**Recognized Organizations:**
- **Local:** SWA (Singapore Weiqi Association), TGA (The Go Academy)
- **Regional:** MWA (Malaysia Weiqi Association), KBA (Korea Baduk Association), Thailand, Vietnam, etc.
- **International:** EGF (European Go Federation)

Kyu players from these organizations enter directly with their corresponding rating. Foreign ranked dan players, however, undergo performance estimation regardless of their ranking organization.

### Players with Other Rankings

Players holding kyu rankings from unrecognized organizations, as well as all foreign dan players, undergo a **12-game performance estimation** period before their rating is finalized.

---

## Performance Estimation for New Players

### When It Applies

Performance estimation is used for:
1. **All foreign dan players** (regardless of ranking organization)
2. **Foreign kyu players** from organizations without comparable ranking standards
3. **Unknown players** with no verifiable ranking
4. **New local dan players** (first joined from 2025, first 6 games only)
5. **Returning inactive players** (after 2+ year break, first 6 games)

**Note:** Items 4 and 5 (local dan players and returning players) only receive dynamic K-factor adjustment for 6 games. They do **not** undergo the 12-game rating correction, as their local ranking history provides sufficient baseline accuracy.

### How It Works

During the estimation period:

1. **Dynamic K-Factor:** 
   
   - **New unknown players** (foreign dan/kyu, unknown ranking): Elevated K-factor that decreases over 12 games:
   
     ```
     K multiplier = 1 + max(0, (12 - gamesPlayed) / 6)
     ```
     
     - 0 games: K multiplier = 3.0×
     - 6 games: K multiplier = 2.0×
     - 12 games: K multiplier = 1.0× (normal)
   
   - **New local dan players and returning inactive players**: Fixed K-factor for first 6 games only:
   
     ```
     K multiplier = 2.0
     ```
     
     These players have reliable local ranking history (SWA/TGA), so they don't need the full dynamic range.

2. **Opponent Protection:** When an uncertain player faces an established player, the established player uses a reduced K-factor (0.5×) to limit rating impact from potentially misrated opponents. Pro players are exempt from this reduction.

3. **Rating Correction:** After 12 games, the system calculates an **estimated true rating** based on performance:
   
   ```
   Weight = sqrt(max(1000, OpponentRating) / 1000)
   Weighted Avg Opponent = sum(OpponentRating × Weight) / sum(Weight)
   Win Rate = sum(Score × Weight) / sum(Weight)
   Rating Diff = 100 × ln(WinRate / (1 - WinRate))    [capped at ±200]
   Estimated Rating = Weighted Avg Opponent + Rating Diff
   ```
   
   - Stronger opponents are weighted more heavily in the calculation
   - Ceiling: Cannot exceed strongest opponent beaten + 150 points
   - Floor: Cannot fall below weakest opponent lost to - 150 points

4. **Partial Correction:** The difference between estimated and initial rating is applied at 50% to avoid over-adjustment, as the dynamic K-factor has already moved the player partway to their true rating.
   
   ```
   Correction = Estimated Rating - Original Initial Rating
   Corrected Rating = Current Rating + (Correction × 0.5)
   ```

### Monitoring Period

New kyu players and uncertain-rated players are "hidden" from main rankings during their first 12 games, visible only on detailed search.

---

## Promotion Bonus

### Purpose

When a player earns a promotion from SWA or an organization with comparable ranking standards, they receive a rating floor to ensure their rating reflects their new rank.

### Why This Is Needed

Without promotion bonus correction, certain players would be unfairly disadvantaged, and their opponents would face mismatched games:

1. **Fast-improving kyu players:** Kyu players often improve rapidly, but many of their training games (e.g., in-class games at go schools) are not included in the rating system.

2. **Handicap competitions:** Some dan upgrading competitions use handicap games, which cannot be rated in an even-game rating system.

3. **Occasional participants:** Some players, especially overseas players, may only participate in local competitions occasionally, causing their rating to lag behind their actual strength.

### Eligibility

- Promotion must be from a recognized organization (SWA, TGA, MWA, KBA, Thailand, Vietnam, EGF)
- Only applies for promotions to **4D and below** (rating < 2500)
- Strong players (5D and above) are considered established and don't need the bonus

### Calculation

The rating floor is set at **half a rank below** the new rank:

```
Rating Floor = New Rank Rating - (Single Rank Difference × 0.5)
```

**Example:** Promotion from 3K to 2K
- 2K rating = 2025
- Single rank difference at this level = 25 points
- Rating floor = 2025 - (25 × 0.5) = 2012.5

If the player's current rating is below this floor, they receive a bonus to bring them up to the floor.

### Special Cases

- **Pro players and foreign players:** Receive the full new ranking rating (no half-rank reduction)
- **Local kyu players receiving promotion:** For local players who entered without an initial ranking, performance estimation is stopped upon promotion, as the promotion validates their strength

---

## Game Processing Order

Games are processed in chronological order. For each game:

1. Check for any promotions that became effective before this game
2. Apply promotion bonus if applicable
3. Calculate expected scores for both players
4. Apply appropriate K-factors (with dynamic adjustments for new players)
5. Update both players' ratings
6. Track game results for performance estimation (if applicable)

---

## Summary

| Player Type | Initial Rating | K-Factor | Estimation Period |
|-------------|---------------|----------|-------------------|
| Local kyu (recognized) | Ranking-based | Normal | Hidden 12 games |
| Local dan (new 2025+) | Ranking-based | Dynamic 6 games | Visible |
| Foreign kyu (recognized) | Ranking-based | Normal | Hidden 12 games |
| Foreign kyu (not recognized) | Ranking-based | Dynamic 12 games | Hidden 12 games |
| Foreign dan | Ranking-based* | Dynamic 12 games | Hidden 12 games |
| Unknown | Default (1700) | Dynamic 12 games | Hidden 12 games |
| Returning (2+ years) | Previous rating | Dynamic 6 games | Visible |
| Established | Current rating | Normal | N/A |

\* Foreign dan receives a 100-point penalty compared to equivalent SWA dan

---

*This document describes the Singapore Weiqi (Go) rating system as implemented in the player ratings application.*

