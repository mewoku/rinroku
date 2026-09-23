using System;

namespace Ronriku.Domain.Puzzles
{
    /// <summary>
    /// Shared scoring for Pattern and Logic trials (Spatial keeps <see cref="SpatialPuzzleScorer"/>, same shape):
    ///   points = max(100, 700 + 300·difficulty + max(0, targetMs − elapsedMs)/100
    ///                   − overParPenalty·max(0, moves − par) − 120·resets), or 0 when unsolved.
    /// </summary>
    public static class TrialScoring
    {
        public const int ResetPenalty = 120;

        public static int TargetMilliseconds(string type, PuzzleDifficulty difficulty) => type switch
        {
            "pattern" => difficulty switch { PuzzleDifficulty.Easy => 20000, PuzzleDifficulty.Standard => 30000, _ => 45000 },
            "logic" => difficulty switch { PuzzleDifficulty.Easy => 30000, PuzzleDifficulty.Standard => 60000, _ => 90000 },
            _ => SpatialPuzzleScorer.TargetMilliseconds(difficulty)
        };

        public static int TimeLimitSeconds(string type, PuzzleDifficulty difficulty) => type switch
        {
            "pattern" => difficulty switch { PuzzleDifficulty.Easy => 45, PuzzleDifficulty.Standard => 60, _ => 90 },
            "logic" => difficulty switch { PuzzleDifficulty.Easy => 60, PuzzleDifficulty.Standard => 120, _ => 180 },
            _ => 90
        };

        public static int Points(PuzzleDifficulty difficulty, bool solved, int elapsedMilliseconds, int targetMilliseconds,
            int moves, int par, int resets, int overParPenalty)
        {
            if (!solved) return 0;
            int difficultyBase = 700 + (int)difficulty * 300;
            int speedBonus = Math.Max(0, targetMilliseconds - Math.Max(0, elapsedMilliseconds)) / 100;
            int penalty = Math.Max(0, moves - par) * overParPenalty + Math.Max(0, resets) * ResetPenalty;
            return Math.Max(100, difficultyBase + speedBonus - penalty);
        }
    }
}
