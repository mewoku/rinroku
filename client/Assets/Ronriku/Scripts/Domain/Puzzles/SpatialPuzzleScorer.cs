using System;

namespace Ronriku.Domain.Puzzles
{
    public readonly struct SpatialAttemptScore
    {
        public readonly bool Solved;
        public readonly int Points;
        public readonly int Moves;
        public readonly int Par;
        public readonly int Resets;
        public readonly int ElapsedMilliseconds;

        public SpatialAttemptScore(bool solved, int points, int moves, int par, int resets, int elapsedMilliseconds)
        {
            Solved = solved;
            Points = points;
            Moves = moves;
            Par = par;
            Resets = resets;
            ElapsedMilliseconds = elapsedMilliseconds;
        }

        public bool AtPar => Solved && Moves <= Par;
    }

    /// <summary>
    /// points = max(100, base + speedBonus - overParPenalty - resetPenalty)
    ///   base          = 700 + 300 × difficulty (1..3)
    ///   speedBonus    = max(0, targetMs - elapsedMs) / 100
    ///   overParPenalty= max(0, moves - par) × 60
    ///   resetPenalty  = resets × 120
    /// </summary>
    public sealed class SpatialPuzzleScorer
    {
        public const int OverParPenalty = 60;
        public const int ResetPenalty = 120;

        public static int TargetMilliseconds(PuzzleDifficulty difficulty) => difficulty switch
        {
            PuzzleDifficulty.Easy => 30000,
            PuzzleDifficulty.Standard => 45000,
            _ => 60000
        };

        public SpatialAttemptScore Calculate(SpatialPuzzleData data, bool solved, int elapsedMilliseconds,
            int moves, int resets)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            elapsedMilliseconds = Math.Max(0, elapsedMilliseconds);
            moves = Math.Max(0, moves);
            resets = Math.Max(0, resets);
            if (!solved) return new SpatialAttemptScore(false, 0, moves, data.Par, resets, elapsedMilliseconds);

            int difficultyBase = 700 + (int)data.Metadata.Difficulty * 300;
            int speedBonus = Math.Max(0, TargetMilliseconds(data.Metadata.Difficulty) - elapsedMilliseconds) / 100;
            int penalty = Math.Max(0, moves - data.Par) * OverParPenalty + resets * ResetPenalty;
            int points = Math.Max(100, difficultyBase + speedBonus - penalty);
            return new SpatialAttemptScore(true, points, moves, data.Par, resets, elapsedMilliseconds);
        }
    }
}
