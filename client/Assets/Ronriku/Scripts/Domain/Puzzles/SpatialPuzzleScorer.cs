using System;

namespace Ronriku.Domain.Puzzles
{
    public readonly struct SpatialAttemptScore
    {
        public readonly bool Solved;
        public readonly int Points;
        public readonly int Rotations;
        public readonly int Resets;
        public readonly int ElapsedMilliseconds;

        public SpatialAttemptScore(bool solved, int points, int rotations, int resets, int elapsedMilliseconds)
        {
            Solved = solved;
            Points = points;
            Rotations = rotations;
            Resets = resets;
            ElapsedMilliseconds = elapsedMilliseconds;
        }
    }

    public sealed class SpatialPuzzleScorer
    {
        public SpatialAttemptScore Calculate(SpatialPuzzleData data, bool solved, int elapsedMilliseconds,
            int rotations, int resets)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            elapsedMilliseconds = Math.Max(0, elapsedMilliseconds);
            rotations = Math.Max(0, rotations);
            resets = Math.Max(0, resets);
            if (!solved) return new SpatialAttemptScore(false, 0, rotations, resets, elapsedMilliseconds);

            int difficulty = 700 + (int)data.Metadata.Difficulty * 300;
            int targetMilliseconds = data.Metadata.Difficulty == PuzzleDifficulty.Easy ? 45000 :
                data.Metadata.Difficulty == PuzzleDifficulty.Standard ? 60000 : 75000;
            int speedBonus = Math.Max(0, (targetMilliseconds - elapsedMilliseconds) / 100);
            int efficiencyPenalty = Math.Max(0, rotations - 1) * 35 + resets * 120;
            int points = Math.Max(100, difficulty + speedBonus - efficiencyPenalty);
            return new SpatialAttemptScore(true, points, rotations, resets, elapsedMilliseconds);
        }
    }
}

