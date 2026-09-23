using System;
using System.Collections.Generic;

namespace Ronriku.Domain.Puzzles
{
    [Serializable]
    public readonly struct GridPoint : IEquatable<GridPoint>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public GridPoint(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(GridPoint other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is GridPoint other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ (Y * 31) ^ Z;
    }

    [Serializable]
    public sealed class SpatialPuzzleData
    {
        public PuzzleMetadata Metadata { get; }
        public IReadOnlyList<GridPoint> Cubes { get; }
        public int StartOrientation { get; }
        public int TargetOrientation { get; }

        public SpatialPuzzleData(PuzzleMetadata metadata, IReadOnlyList<GridPoint> cubes,
            int startOrientation, int targetOrientation)
        {
            Metadata = metadata;
            Cubes = cubes;
            StartOrientation = startOrientation;
            TargetOrientation = targetOrientation;
        }
    }

    public sealed class SpatialPuzzleGenerator : IPuzzleGenerator<SpatialPuzzleData>
    {
        public const int CurrentRulesVersion = 1;

        public SpatialPuzzleData Generate(long seed, PuzzleDifficulty difficulty, long variantSeed)
        {
            var random = new DeterministicRandom(unchecked((ulong)seed));
            var cubes = new List<GridPoint>
            {
                new GridPoint(0, 0, 0),
                new GridPoint(1, 0, 0),
                new GridPoint(0, 1, 0),
                new GridPoint(0, 0, 1)
            };

            int extra = difficulty == PuzzleDifficulty.Easy ? 0 : difficulty == PuzzleDifficulty.Standard ? 1 : 2;
            GridPoint[] candidates =
            {
                new GridPoint(2, 0, 0), new GridPoint(1, 1, 0), new GridPoint(0, 2, 0),
                new GridPoint(1, 0, 1), new GridPoint(0, 1, 1), new GridPoint(0, 0, 2)
            };
            for (int i = 0; i < extra; i++)
            {
                int index = random.NextInt(candidates.Length - i);
                cubes.Add(candidates[index]);
                candidates[index] = candidates[candidates.Length - 1 - i];
            }

            int start = (int)(unchecked((ulong)variantSeed) & 3UL);
            int targetOffset = 1 + random.NextInt(3);
            int target = (start + targetOffset) & 3;
            string hash = ContentHash(seed, variantSeed, difficulty, cubes, start, target);
            var metadata = new PuzzleMetadata(
                $"spatial-{seed:x16}", 1, "spatial", seed, variantSeed, difficulty,
                CurrentRulesVersion, "ROTATE THE STRUCTURE TO MATCH THE TARGET", 90,
                new[] { "Spatial", "Planning", "Speed" }, hash);
            return new SpatialPuzzleData(metadata, cubes, start, target);
        }

        private static string ContentHash(long seed, long variantSeed, PuzzleDifficulty difficulty,
            IEnumerable<GridPoint> cubes, int start, int target)
        {
            ulong hash = 14695981039346656037UL;
            void Add(long value)
            {
                unchecked
                {
                    ulong raw = (ulong)value;
                    for (int i = 0; i < 8; i++)
                    {
                        hash ^= (byte)(raw >> (i * 8));
                        hash *= 1099511628211UL;
                    }
                }
            }
            Add(seed); Add(variantSeed); Add((int)difficulty); Add(start); Add(target);
            foreach (var point in cubes) { Add(point.X); Add(point.Y); Add(point.Z); }
            return hash.ToString("x16");
        }
    }

    public sealed class SpatialPuzzleValidator : IPuzzleValidator<SpatialPuzzleData, int>
    {
        public bool IsCorrect(SpatialPuzzleData data, int answer) =>
            data != null && (answer & 3) == data.TargetOrientation;
    }

    internal struct DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(ulong seed) => _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;

        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            ulong value = _state;
            value ^= value >> 12;
            value ^= value << 25;
            value ^= value >> 27;
            _state = value;
            return (int)((value * 2685821657736338717UL) % (ulong)exclusiveMax);
        }
    }
}

